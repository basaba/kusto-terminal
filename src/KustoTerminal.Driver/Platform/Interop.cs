using System.Runtime.InteropServices;

namespace KustoTerminal.Driver.Platform;

/// <summary>
/// P/Invoke declarations for Unix terminal operations (macOS + Linux).
/// Uses raw byte buffers for termios to avoid struct layout differences
/// between macOS ARM64 (8-byte fields) and Linux x86_64 (4-byte fields).
/// </summary>
internal static partial class Interop
{
    private const string LibC = "libc";

    // termios constants
    internal const int STDIN_FILENO = 0;
    internal const int STDOUT_FILENO = 1;

    // tcsetattr actions
    internal const int TCSANOW = 0;
    internal const int TCSAFLUSH = 2;

    // ioctl request for terminal window size
    // macOS: 0x40087468, Linux: 0x5413
    internal static uint TIOCGWINSZ => RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        ? 0x40087468u
        : 0x5413u;

    // Signal numbers
    internal const int SIGWINCH = 28;
    internal const int SIGTSTP = 18;
    internal const int SIGCONT = 19;

    // Termios buffer size — large enough for both platforms
    // macOS termios: ~72 bytes, Linux termios: ~60 bytes
    internal const int TermiosBufferSize = 128;

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial int tcgetattr(int fd, Span<byte> termios);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial int tcsetattr(int fd, int optionalActions, ReadOnlySpan<byte> termios);

    [LibraryImport(LibC)]
    internal static partial void cfmakeraw(Span<byte> termios);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial nint read(int fd, Span<byte> buf, nuint count);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial nint write(int fd, ReadOnlySpan<byte> buf, nuint count);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial int ioctl(int fd, uint request, ref WinSize ws);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial int poll(Span<PollFd> fds, uint nfds, int timeout);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial int pipe(Span<int> pipefd);

    [LibraryImport(LibC, SetLastError = true)]
    internal static partial int close(int fd);

    // Signal handling — use sigaction-style via PosixSignalRegistration in .NET 8
    // (avoids P/Invoke issues with function pointers across platforms)

    [StructLayout(LayoutKind.Sequential)]
    internal struct WinSize
    {
        public ushort ws_row;
        public ushort ws_col;
        public ushort ws_xpixel;
        public ushort ws_ypixel;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PollFd
    {
        public int fd;
        public short events;
        public short revents;
    }

    // poll() event flags
    internal const short POLLIN = 0x0001;
    internal const short POLLHUP = 0x0010;
    internal const short POLLERR = 0x0008;
}
