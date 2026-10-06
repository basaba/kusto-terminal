using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using KustoTerminal.Core.Services;

[SupportedOSPlatform("linux")]
internal static class LinuxSocketTests
{
    private static string[] Arguments(string path, int pid) =>
        ["--shell-managed", "--cluster", "https://example.kusto.windows.net", "--database", "db",
         "--tenant", "opaque-tenant", "--shell-socket", path, "--shell-parent-pid", pid.ToString()];

    internal static async Task RunAsync()
    {
        // Keep the native sockaddr_un path below 108 bytes, without using shared temporary directories.
        var directory = Path.Combine(Directory.GetCurrentDirectory(), ".kt-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var path = Path.Combine(directory, "s");
        try
        {
            var options = ManagedShellOptions.Parse(Arguments(path, Environment.ProcessId))!;
            Check(options.ClusterUri == "https://example.kusto.windows.net", "Linux socket CLI accepted");
            foreach (var invalid in new[] { "s", "@abstract", "//host/s", "file:///socket",
                "/a/../s", "/a/./s", "/a//s", "/a\\s", "/a/s/", "/a/\0s", "/" + new string('x', 107),
                "/" + new string('é', 54) })
                await RejectAsync(() => Task.FromResult(ManagedShellOptions.Parse(Arguments(invalid, 123))), "unsafe socket path");
            await RejectAsync(() => Task.FromResult(ManagedShellOptions.Parse(
                Arguments(path, 123).Concat(["--shell-pipe", "kt-11111111-2222-3333-4444-555555555555"]).ToArray())),
                "ambiguous transports");
            await RejectAsync(() => Task.FromResult(ManagedShellOptions.Parse(
                Arguments(path, 123).Concat(["--shell-socket", path]).ToArray())), "duplicate socket");
            await RejectAsync(() => ManagedTokenChannel.ConnectAsync(options), "missing server");
            using (var peer = await StartPeerAsync(path, "disconnect"))
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                await CancelAsync(() => ManagedTokenChannel.ConnectAsync(
                    ManagedShellOptions.Parse(Arguments(path, peer.Id))!, cancelled.Token));
            }

            using (var peer = await StartPeerAsync(path, "renew"))
            {
                using var channel = await ManagedTokenChannel.ConnectAsync(
                    ManagedShellOptions.Parse(Arguments(path, peer.Id))!);
                using var session = new ManagedSession(channel);
                var builders = await Task.WhenAll(Enumerable.Range(0, 4)
                    .Select(_ => session.CreateConnectionStringBuilderAsync(session.CreateConnection())));
                Check(builders.Select(b => b.UserToken).SequenceEqual(
                    new[] { "synthetic-0", "synthetic-1", "synthetic-2", "synthetic-3" }), "real socket renewal/serialization");
                Check(builders.All(b => b.FederatedSecurity && !b.DstsFederatedSecurity &&
                    b.InitialCatalog == "db" && b.DataSource == "https://example.kusto.windows.net"), "Linux MSAL SDK mode");
            }
            using (var peer = await StartPeerAsync(path, "disconnect"))
                await RejectAsync(() => ManagedTokenChannel.ConnectAsync(
                    ManagedShellOptions.Parse(Arguments(path, Environment.ProcessId))!), "different peer PID");

            foreach (var mode in new[] { "disconnect", "truncated", "dsts", "cancel" })
            {
                using var peer = await StartPeerAsync(path, mode);
                using var channel = await ManagedTokenChannel.ConnectAsync(ManagedShellOptions.Parse(Arguments(path, peer.Id))!);
                using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
                if (mode == "cancel")
                    await CancelAsync(() => channel.AcquireAsync(cancellation.Token));
                else
                    await RejectAsync(() => channel.AcquireAsync(), mode);
                await RejectAsync(() => channel.AcquireAsync(), "failed channel cannot be reused");
            }
            using (var peer = await StartPeerAsync(path, "disconnect"))
            {
                File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite |
                    UnixFileMode.UserExecute | UnixFileMode.GroupExecute);
                await RejectAsync(() => ManagedTokenChannel.ConnectAsync(
                    ManagedShellOptions.Parse(Arguments(path, peer.Id))!), "nonprivate socket directory");
                File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            var link = directory + "-link";
            Directory.CreateSymbolicLink(link, directory);
            try
            {
                using var peer = await StartPeerAsync(path, "disconnect");
                await RejectAsync(() => ManagedTokenChannel.ConnectAsync(
                    ManagedShellOptions.Parse(Arguments(Path.Combine(link, "s"), peer.Id))!), "symlink socket path");
            }
            finally { Directory.Delete(link); }
            var socketLink = Path.Combine(directory, "alias");
            using (var peer = await StartPeerAsync(path, "disconnect"))
            {
                File.CreateSymbolicLink(socketLink, path);
                await RejectAsync(() => ManagedTokenChannel.ConnectAsync(
                    ManagedShellOptions.Parse(Arguments(socketLink, peer.Id))!), "symlink socket file");
            }
            if (GetEffectiveUserId() == 0)
            {
                // Root can exercise a genuine foreign-UID server without mocking SO_PEERCRED.
                using var peer = await StartPeerAsync(path, "foreign-uid");
                await RejectAsync(() => ManagedTokenChannel.ConnectAsync(
                    ManagedShellOptions.Parse(Arguments(path, peer.Id))!), "different peer UID");
            }
            else
                Console.WriteLine("SKIP: real wrong-UID peer requires root; real different-PID peer is exercised.");
            Console.WriteLine("PASS: Linux real separate-process sockets, peer PID, path safety, renewal, MSAL, dSTS rejection, disconnect and cancellation.");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static async Task<Peer> StartPeerAsync(string path, string mode)
    {
        File.Delete(path);
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--socket-peer");
        start.ArgumentList.Add(path);
        start.ArgumentList.Add(mode);
        var peer = new Peer(Process.Start(start)!);
        try
        {
            Check(await peer.Process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)) == "READY",
                "socket peer startup: " + (peer.Process.HasExited ? await peer.Process.StandardError.ReadToEndAsync() : ""));
            return peer;
        }
        catch { peer.Dispose(); throw; }
    }

    internal static async Task RunPeerAsync(string path, string mode)
    {
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        var uid = GetEffectiveUserId();
        if (mode == "foreign-uid")
            Check(SetEffectiveUserId(65534) == 0, "Cannot create foreign-UID fixture.");
        try { listener.Listen(1); }
        finally
        {
            // Linux snapshots peer credentials at listen(). Restore filesystem access for the .NET runtime.
            if (mode == "foreign-uid") Check(SetEffectiveUserId(uid) == 0, "Cannot restore fixture UID.");
        }
        Console.WriteLine("READY");
        using var socket = await listener.AcceptAsync();
        using var stream = new NetworkStream(socket);
        if (mode is "disconnect" or "foreign-uid") return;
        if (mode == "cancel")
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            return;
        }
        for (var i = 0; i < (mode == "renew" ? 4 : 1); i++)
        {
            var prefix = new byte[4];
            await stream.ReadExactlyAsync(prefix);
            var size = BinaryPrimitives.ReadInt32LittleEndian(prefix);
            Check(size is > 0 and <= 4096, "request size");
            var request = new byte[size];
            await stream.ReadExactlyAsync(request);
            using var json = JsonDocument.Parse(request);
            Check(json.RootElement.GetProperty("Version").GetInt32() == 1 &&
                json.RootElement.GetProperty("ClusterUri").GetString() == "https://example.kusto.windows.net",
                "real peer request target");
            if (mode == "truncated")
            {
                await stream.WriteAsync(new byte[] { 5, 0, 0, 0, 123 });
                return;
            }
            var response = JsonSerializer.SerializeToUtf8Bytes(new
            {
                Version = 1, RequestId = json.RootElement.GetProperty("RequestId").GetString(),
                Scheme = mode == "dsts" ? 1 : 0, AccessToken = $"synthetic-{i}",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1), ErrorCode = ""
            });
            BinaryPrimitives.WriteInt32LittleEndian(prefix, response.Length);
            // Real network fragmentation, not a stream double.
            foreach (var b in prefix) await stream.WriteAsync(new byte[] { b });
            for (var offset = 0; offset < response.Length; offset += 3)
                await stream.WriteAsync(response.AsMemory(offset, Math.Min(3, response.Length - offset)));
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    private static async Task RejectAsync(Func<Task> action, string message)
    {
        try { await action().WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception ex) when (ex is not TimeoutException) { return; }
        throw new Exception("Accepted " + message);
    }

    private static async Task CancelAsync(Func<Task> action)
    {
        try { await action().WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (OperationCanceledException) { return; }
        throw new Exception("Cancellation was not honored.");
    }

    private sealed class Peer(Process process) : IDisposable
    {
        internal Process Process => process;
        internal int Id => process.Id;
        public void Dispose()
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            process.WaitForExit();
            process.Dispose();
        }
    }

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint GetEffectiveUserId();
    [DllImport("libc", EntryPoint = "seteuid")]
    private static extern int SetEffectiveUserId(uint uid);
}
