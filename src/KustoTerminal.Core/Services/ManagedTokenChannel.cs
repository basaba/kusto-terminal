using System.Buffers.Binary;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace KustoTerminal.Core.Services;

public enum ManagedTokenScheme { Msal = 0, Dsts = 1 }

// Deliberately not a record: generated ToString must not expose the token.
public sealed class ManagedCredential
{
    public ManagedTokenScheme Scheme { get; }
    public string AccessToken { get; }
    public DateTimeOffset ExpiresOn { get; }
    internal ManagedCredential(ManagedTokenScheme scheme, string token, DateTimeOffset expiresOn)
        => (Scheme, AccessToken, ExpiresOn) = (scheme, token, expiresOn);
}

/// <summary>Owns a serialized, fail-closed channel. Credentials are acquired anew for each SDK operation.</summary>
public sealed class ManagedTokenChannel : IDisposable
{
    private readonly Stream _stream;
    private readonly ManagedShellOptions _options;
    private readonly SemaphoreSlim _gate = new(1);
    private readonly CancellationTokenSource _closed = new();
    private int _disposed;
    internal ManagedShellOptions Options => _options;

    internal ManagedTokenChannel(Stream stream, ManagedShellOptions options)
        => (_stream, _options) = (stream, options);

    public static async Task<ManagedTokenChannel> ConnectAsync(ManagedShellOptions options,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            if (OperatingSystem.IsWindows() && options.PipeName != null)
            {
                var pipe = new NamedPipeClientStream(".", options.PipeName, PipeDirection.InOut,
                    PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
                try
                {
                    await pipe.ConnectAsync(timeout.Token);
                    if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var pid) || pid != options.ParentProcessId)
                        throw new InvalidOperationException("Managed shell peer verification failed.");
                    return new(pipe, options);
                }
                catch { pipe.Dispose(); throw; }
            }
            if (OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64 &&
                options.SocketPath != null && GetGlibcVersion() != IntPtr.Zero)
            {
                var path = options.SocketPath;
                var directory = new DirectoryInfo(Path.GetDirectoryName(path)!);
                if (File.GetUnixFileMode(directory.FullName) !=
                    (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute) ||
                    new FileInfo(path).LinkTarget != null)
                    throw new InvalidOperationException("Unsafe managed shell socket path.");
                for (DirectoryInfo? ancestor = directory; ancestor != null; ancestor = ancestor.Parent)
                    if (ancestor.LinkTarget != null)
                        throw new InvalidOperationException("Unsafe managed shell socket path.");

                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(path), timeout.Token);
                    var size = (uint)Marshal.SizeOf<LinuxPeerCredentials>();
                    if (GetPeerCredentials(socket.SafeHandle, 1 /* SOL_SOCKET */, 17 /* SO_PEERCRED */,
                        out var peer, ref size) != 0 || size != Marshal.SizeOf<LinuxPeerCredentials>() ||
                        peer.ProcessId != options.ParentProcessId || peer.UserId != GetEffectiveUserId())
                        throw new InvalidOperationException("Managed shell peer verification failed.");
                    return new(new NetworkStream(socket, ownsSocket: true), options);
                }
                catch { socket.Dispose(); throw; }
            }
            throw new PlatformNotSupportedException("Managed shell requires Windows pipes or Linux x64 glibc sockets.");
        }
        catch
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);
            throw new InvalidOperationException("Unable to connect to the managed shell parent.");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

    [StructLayout(LayoutKind.Sequential)]
    private struct LinuxPeerCredentials
    {
        public int ProcessId;
        public uint UserId;
        public uint GroupId;
    }

    [DllImport("libc", EntryPoint = "getsockopt", SetLastError = true)]
    private static extern int GetPeerCredentials(SafeSocketHandle socket, int level, int option,
        out LinuxPeerCredentials credentials, ref uint length);

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint GetEffectiveUserId();

    [DllImport("libc", EntryPoint = "gnu_get_libc_version")]
    private static extern IntPtr GetGlibcVersion();

    public async Task<ManagedCredential> AcquireAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _closed.Token);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        // Closing on cancellation prevents reuse of a partially consumed frame.
        using var registration = timeout.Token.Register(Dispose);
        var entered = false;
        try
        {
            await _gate.WaitAsync(timeout.Token);
            entered = true;
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            var requestId = Guid.NewGuid().ToString("N");
            var payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                Version = 1, RequestId = requestId, ClusterUri = _options.ClusterUri
            });
            if (payload.Length > 4096)
                throw new InvalidDataException();
            var prefix = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
            await _stream.WriteAsync(prefix, timeout.Token);
            await _stream.WriteAsync(payload, timeout.Token);
            await _stream.FlushAsync(timeout.Token);
            await _stream.ReadExactlyAsync(prefix, timeout.Token);
            var size = BinaryPrimitives.ReadInt32LittleEndian(prefix);
            if (size is <= 0 or > 65536)
                throw new InvalidDataException();
            var response = new byte[size];
            try
            {
                await _stream.ReadExactlyAsync(response, timeout.Token);
                using var document = JsonDocument.Parse(response);
                var root = document.RootElement;
                var expected = new HashSet<string>(StringComparer.Ordinal)
                    { "Version", "RequestId", "Scheme", "AccessToken", "ExpiresOn", "ErrorCode" };
                foreach (var field in root.EnumerateObject())
                    if (!expected.Remove(field.Name)) throw new InvalidDataException();
                if (expected.Count != 0 || root.GetProperty("Version").GetInt32() != 1 ||
                    root.GetProperty("RequestId").GetString() != requestId ||
                    root.GetProperty("ErrorCode").GetString() != "")
                    throw new InvalidDataException();
                var scheme = root.GetProperty("Scheme").GetInt32();
                var token = root.GetProperty("AccessToken").GetString();
                var expiryText = root.GetProperty("ExpiresOn").GetString();
                if (expiryText == null || (!expiryText.EndsWith('Z') &&
                    (expiryText.Length < 6 || expiryText[^6] is not ('+' or '-'))))
                    throw new InvalidDataException();
                var expiry = root.GetProperty("ExpiresOn").GetDateTimeOffset();
                if (scheme is not (0 or 1) || (scheme == 1 && !OperatingSystem.IsWindows()) ||
                    string.IsNullOrEmpty(token) ||
                    token.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)) ||
                    expiry <= DateTimeOffset.UtcNow.AddMinutes(2))
                    throw new InvalidDataException();
                return new((ManagedTokenScheme)scheme, token, expiry);
            }
            finally { Array.Clear(response); }
        }
        catch
        {
            Dispose();
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);
            throw new InvalidOperationException("Managed shell authentication failed; reconnect from the parent shell.");
        }
        finally { if (entered) _gate.Release(); }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _closed.Cancel();
        _stream.Dispose();
    }
}
