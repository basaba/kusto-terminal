using System.Runtime.InteropServices;
using System.Text;

namespace KustoTerminal.Driver.Platform;

/// <summary>
/// Platform abstraction for low-level terminal operations.
/// </summary>
public interface IPlatformTerminal : IDisposable
{
    /// <summary>Enable raw terminal mode (disable line buffering, echo, etc.).</summary>
    void EnableRawMode();

    /// <summary>Restore original terminal mode.</summary>
    void RestoreMode();

    /// <summary>Write a string to stdout as efficiently as possible.</summary>
    void WriteToStdout(string data);

    /// <summary>Write a byte span to stdout.</summary>
    void WriteToStdout(ReadOnlySpan<byte> data);

    /// <summary>Get the current terminal size.</summary>
    (int rows, int cols) GetWindowSize();

    /// <summary>Register a callback for terminal resize events (SIGWINCH on Unix).</summary>
    void OnResize(Action<int, int> callback);
}

/// <summary>
/// Unix (Linux/macOS) terminal implementation using P/Invoke to libc.
/// </summary>
public sealed class UnixPlatformTerminal : IPlatformTerminal
{
    private Termios _originalTermios;
    private bool _rawMode;
    private Action<int, int>? _resizeCallback;
    private static UnixPlatformTerminal? _instance;
    private readonly Stream _stdout;

    public UnixPlatformTerminal()
    {
        _stdout = Console.OpenStandardOutput();
        _instance = this;
    }

    public void EnableRawMode()
    {
        if (_rawMode) return;

        // Save original terminal settings
        tcgetattr(0, out _originalTermios);

        var raw = _originalTermios;

        // Input flags: disable break signal, CR→NL, parity, strip, flow control
        raw.c_iflag &= ~(BRKINT | ICRNL | INPCK | ISTRIP | IXON);

        // Output flags: disable post-processing
        raw.c_oflag &= ~(OPOST);

        // Control flags: set 8-bit chars
        raw.c_cflag |= CS8;

        // Local flags: disable echo, canonical mode, extended input, signals
        raw.c_lflag &= ~(ECHO | ICANON | IEXTEN | ISIG);

        // Read timeout settings: read returns after 0 bytes with 100ms timeout
        raw.c_cc_VMIN = 0;
        raw.c_cc_VTIME = 1; // 100ms timeout

        tcsetattr(0, TCSAFLUSH, ref raw);
        _rawMode = true;
    }

    public void RestoreMode()
    {
        if (!_rawMode) return;
        tcsetattr(0, TCSAFLUSH, ref _originalTermios);
        _rawMode = false;
    }

    public void WriteToStdout(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        _stdout.Write(bytes, 0, bytes.Length);
        _stdout.Flush();
    }

    public void WriteToStdout(ReadOnlySpan<byte> data)
    {
        _stdout.Write(data);
        _stdout.Flush();
    }

    public (int rows, int cols) GetWindowSize()
    {
        var ws = new Winsize();
        if (ioctl(1, TIOCGWINSZ, ref ws) == 0)
        {
            return (ws.ws_row, ws.ws_col);
        }
        // Fallback
        return (Console.WindowHeight, Console.WindowWidth);
    }

    public void OnResize(Action<int, int> callback)
    {
        _resizeCallback = callback;
        RegisterSigwinch();
    }

    private void RegisterSigwinch()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            PosixSignalRegistration.Create(PosixSignal.SIGWINCH, ctx =>
            {
                ctx.Cancel = true;
                var (rows, cols) = GetWindowSize();
                _resizeCallback?.Invoke(rows, cols);
            });
        }
    }

    public void Dispose()
    {
        RestoreMode();
        _stdout.Dispose();
    }

    // --- P/Invoke declarations ---

    // termios flags (Linux values — macOS uses same names but different values,
    // however .NET's runtime handles the mapping)
    private const uint BRKINT = 0x0002;
    private const uint ICRNL = 0x0100;
    private const uint INPCK = 0x0010;
    private const uint ISTRIP = 0x0020;
    private const uint IXON = 0x0400;
    private const uint OPOST = 0x0001;
    private const uint CS8 = 0x0030;
    private const uint ECHO = 0x0008;
    private const uint ICANON = 0x0002;
    private const uint IEXTEN = 0x8000;
    private const uint ISIG = 0x0001;
    private const int TCSAFLUSH = 2;

    // ioctl for TIOCGWINSZ
    private static readonly nuint TIOCGWINSZ = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        ? (nuint)0x40087468 : (nuint)0x5413;

    [StructLayout(LayoutKind.Sequential)]
    private struct Termios
    {
        public uint c_iflag;
        public uint c_oflag;
        public uint c_cflag;
        public uint c_lflag;
        // cc array varies by platform; we only need VMIN and VTIME
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public byte[] c_cc;
        public uint c_ispeed;
        public uint c_ospeed;

        public byte c_cc_VMIN
        {
            get => c_cc != null && c_cc.Length > 6 ? c_cc[6] : (byte)0;
            set { if (c_cc != null && c_cc.Length > 6) c_cc[6] = value; }
        }

        public byte c_cc_VTIME
        {
            get => c_cc != null && c_cc.Length > 5 ? c_cc[5] : (byte)0;
            set { if (c_cc != null && c_cc.Length > 5) c_cc[5] = value; }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Winsize
    {
        public ushort ws_row;
        public ushort ws_col;
        public ushort ws_xpixel;
        public ushort ws_ypixel;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int tcgetattr(int fd, out Termios termios);

    [DllImport("libc", SetLastError = true)]
    private static extern int tcsetattr(int fd, int optionalActions, ref Termios termios);

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, nuint request, ref Winsize winsize);
}

/// <summary>
/// Windows terminal implementation using Console API P/Invoke.
/// </summary>
public sealed class WindowsPlatformTerminal : IPlatformTerminal
{
    private uint _originalInputMode;
    private uint _originalOutputMode;
    private bool _rawMode;
    private Action<int, int>? _resizeCallback;
    private readonly Stream _stdout;

    public WindowsPlatformTerminal()
    {
        _stdout = Console.OpenStandardOutput();
    }

    public void EnableRawMode()
    {
        if (_rawMode || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var hIn = GetStdHandle(STD_INPUT_HANDLE);
        var hOut = GetStdHandle(STD_OUTPUT_HANDLE);

        GetConsoleMode(hIn, out _originalInputMode);
        GetConsoleMode(hOut, out _originalOutputMode);

        // Enable VT processing on output
        uint outputMode = _originalOutputMode | ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
        SetConsoleMode(hOut, outputMode);

        // Enable VT input + disable line input and echo
        uint inputMode = (_originalInputMode | ENABLE_VIRTUAL_TERMINAL_INPUT)
            & ~(ENABLE_LINE_INPUT | ENABLE_ECHO_INPUT | ENABLE_PROCESSED_INPUT);
        SetConsoleMode(hIn, inputMode);

        _rawMode = true;
    }

    public void RestoreMode()
    {
        if (!_rawMode || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var hIn = GetStdHandle(STD_INPUT_HANDLE);
        var hOut = GetStdHandle(STD_OUTPUT_HANDLE);

        SetConsoleMode(hIn, _originalInputMode);
        SetConsoleMode(hOut, _originalOutputMode);

        _rawMode = false;
    }

    public void WriteToStdout(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        _stdout.Write(bytes, 0, bytes.Length);
        _stdout.Flush();
    }

    public void WriteToStdout(ReadOnlySpan<byte> data)
    {
        _stdout.Write(data);
        _stdout.Flush();
    }

    public (int rows, int cols) GetWindowSize()
    {
        return (Console.WindowHeight, Console.WindowWidth);
    }

    public void OnResize(Action<int, int> callback)
    {
        _resizeCallback = callback;
        // Windows resize is detected via polling or WINDOW_BUFFER_SIZE_EVENT in input stream
        // For now, we detect it during input processing
    }

    /// <summary>Called from input processing when a resize event is detected.</summary>
    internal void NotifyResize()
    {
        var (rows, cols) = GetWindowSize();
        _resizeCallback?.Invoke(rows, cols);
    }

    public void Dispose()
    {
        RestoreMode();
        _stdout.Dispose();
    }

    // --- Windows Console P/Invoke ---
    private const int STD_INPUT_HANDLE = -10;
    private const int STD_OUTPUT_HANDLE = -11;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
    private const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;
    private const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;
    private const uint ENABLE_LINE_INPUT = 0x0002;
    private const uint ENABLE_ECHO_INPUT = 0x0004;
    private const uint ENABLE_PROCESSED_INPUT = 0x0001;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}

/// <summary>
/// Factory to create the appropriate platform terminal for the current OS.
/// </summary>
public static class PlatformTerminalFactory
{
    public static IPlatformTerminal Create()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsPlatformTerminal();
        return new UnixPlatformTerminal();
    }
}
