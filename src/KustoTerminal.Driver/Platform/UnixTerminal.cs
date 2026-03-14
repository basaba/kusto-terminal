using System.Runtime.InteropServices;
using System.Text;
using static KustoTerminal.Driver.Platform.Interop;

namespace KustoTerminal.Driver.Platform;

/// <summary>
/// Unix (macOS + Linux) terminal implementation.
/// Provides raw mode, direct fd I/O, and resize handling.
/// </summary>
internal sealed class UnixTerminal : IPlatformTerminal
{
    private readonly byte[] _originalTermios = new byte[TermiosBufferSize];
    private bool _rawMode;
    private Action<int, int>? _resizeHandler;
    private PosixSignalRegistration? _sigwinchRegistration;
    private bool _disposed;

    public int StdinFd => STDIN_FILENO;

    public bool SupportsTrueColor { get; }

    public UnixTerminal()
    {
        var colorTerm = Environment.GetEnvironmentVariable("COLORTERM");
        SupportsTrueColor = colorTerm is "truecolor" or "24bit";
    }

    public void EnterRawMode()
    {
        if (_rawMode) return;

        // Save original state
        if (tcgetattr(STDIN_FILENO, _originalTermios) != 0)
            throw new InvalidOperationException(
                $"tcgetattr failed: {Marshal.GetLastPInvokeError()}");

        // Create raw mode copy
        var rawTermios = new byte[TermiosBufferSize];
        _originalTermios.CopyTo(rawTermios.AsSpan());
        cfmakeraw(rawTermios);

        if (tcsetattr(STDIN_FILENO, TCSAFLUSH, rawTermios) != 0)
            throw new InvalidOperationException(
                $"tcsetattr failed: {Marshal.GetLastPInvokeError()}");

        _rawMode = true;

        // Register SIGWINCH handler via .NET 8 PosixSignalRegistration
        _sigwinchRegistration = PosixSignalRegistration.Create(
            PosixSignal.SIGWINCH, ctx =>
            {
                var (cols, rows) = GetTerminalSize();
                _resizeHandler?.Invoke(cols, rows);
            });
    }

    public void ExitRawMode()
    {
        if (!_rawMode) return;

        _sigwinchRegistration?.Dispose();
        _sigwinchRegistration = null;

        tcsetattr(STDIN_FILENO, TCSAFLUSH, _originalTermios);
        _rawMode = false;
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return;

        int offset = 0;
        while (offset < data.Length)
        {
            var written = write(STDOUT_FILENO, data[offset..], (nuint)(data.Length - offset));
            if (written < 0)
            {
                var err = Marshal.GetLastPInvokeError();
                if (err == 4) continue; // EINTR — retry
                throw new InvalidOperationException($"write failed: {err}");
            }
            offset += (int)written;
        }
    }

    public int Read(Span<byte> buffer)
    {
        var result = read(STDIN_FILENO, buffer, (nuint)buffer.Length);
        if (result < 0)
        {
            var err = Marshal.GetLastPInvokeError();
            if (err == 11 || err == 35) return 0; // EAGAIN/EWOULDBLOCK
            if (err == 4) return 0; // EINTR
            throw new InvalidOperationException($"read failed: {err}");
        }
        return (int)result;
    }

    public (int cols, int rows) GetTerminalSize()
    {
        // On macOS ARM64, ioctl is variadic and crashes via P/Invoke (SIGSEGV).
        // Use Console API directly — it's reliable on all platforms.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                return (Console.WindowWidth, Console.WindowHeight);
            }
            catch
            {
                return (80, 24);
            }
        }

        // Linux: ioctl is safe (not variadic)
        var ws = new WinSize();
        if (ioctl(STDOUT_FILENO, TIOCGWINSZ, ref ws) == 0 && ws.ws_col > 0 && ws.ws_row > 0)
        {
            return (ws.ws_col, ws.ws_row);
        }

        try
        {
            return (Console.WindowWidth, Console.WindowHeight);
        }
        catch
        {
            return (80, 24);
        }
    }

    public void OnResize(Action<int, int> handler)
    {
        _resizeHandler = handler;
    }

    public (int readFd, int writeFd) CreateWakeupPipe()
    {
        Span<int> fds = stackalloc int[2];
        if (pipe(fds) != 0)
            throw new InvalidOperationException(
                $"pipe failed: {Marshal.GetLastPInvokeError()}");
        return (fds[0], fds[1]);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ExitRawMode();
    }
}
