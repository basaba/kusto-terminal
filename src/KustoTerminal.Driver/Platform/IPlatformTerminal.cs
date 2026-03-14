namespace KustoTerminal.Driver.Platform;

/// <summary>
/// Platform abstraction for terminal I/O operations.
/// </summary>
internal interface IPlatformTerminal : IDisposable
{
    /// <summary>Enter raw mode (disable echo, canonical, signals)</summary>
    void EnterRawMode();

    /// <summary>Restore the original terminal state</summary>
    void ExitRawMode();

    /// <summary>Write bytes directly to stdout</summary>
    void Write(ReadOnlySpan<byte> data);

    /// <summary>Read available bytes from stdin (non-blocking if no data)</summary>
    int Read(Span<byte> buffer);

    /// <summary>Get current terminal dimensions</summary>
    (int cols, int rows) GetTerminalSize();

    /// <summary>Whether the terminal supports true color (24-bit)</summary>
    bool SupportsTrueColor { get; }

    /// <summary>Register a callback for terminal resize events</summary>
    void OnResize(Action<int, int> handler);

    /// <summary>File descriptor for stdin (used by poll)</summary>
    int StdinFd { get; }

    /// <summary>Create a self-pipe for waking poll from another thread</summary>
    (int readFd, int writeFd) CreateWakeupPipe();
}
