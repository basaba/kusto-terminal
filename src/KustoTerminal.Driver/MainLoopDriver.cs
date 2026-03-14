using System.Diagnostics;
using System.Reflection;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using KustoTerminal.Driver.Input;
using KustoTerminal.Driver.Platform;
using static KustoTerminal.Driver.Platform.Interop;

namespace KustoTerminal.Driver;

/// <summary>
/// DispatchProxy that implements the internal IMainLoopDriver interface.
/// Terminal.Gui's IMainLoopDriver is internal, so we use DispatchProxy to
/// create a runtime proxy that delegates to our actual implementation.
///
/// Uses adaptive poll timeout for low-latency input while minimizing idle CPU:
///   Active (input detected): 2ms poll → sub-5ms keystroke latency
///   Idle (no input for 200ms): 50ms poll → low CPU usage
/// </summary>
public class KustoMainLoopProxy : DispatchProxy
{
    private KustoConsoleDriver? _driver;
    private IPlatformTerminal? _terminal;
    private MainLoop? _mainLoop;

    private int _wakeupReadFd;
    private int _wakeupWriteFd;

    private readonly byte[] _inputBuffer = new byte[1024];
    private readonly AnsiSequenceReader _sequenceReader = new();

    // Adaptive poll timeout: fast when active, slow when idle
    private const int ActivePollMs = 2;
    private const int IdlePollMs = 50;
    private const long IdleAfterTicks = 200 * TimeSpan.TicksPerMillisecond;
    private long _lastActivityTicks = Stopwatch.GetTimestamp();

    /// <summary>
    /// Configure the proxy after creation (DispatchProxy.Create uses parameterless ctor).
    /// </summary>
    internal KustoMainLoopProxy() { }

    internal KustoMainLoopProxy(KustoConsoleDriver driver, IPlatformTerminal terminal)
    {
        _driver = driver;
        _terminal = terminal;
    }

    /// <summary>
    /// Create an instance of IMainLoopDriver that delegates to our implementation.
    /// Uses reflection since IMainLoopDriver is internal.
    /// </summary>
    internal static object CreateProxy(KustoConsoleDriver driver, IPlatformTerminal terminal)
    {
        var imlType = typeof(MainLoop).Assembly.GetType("Terminal.Gui.App.IMainLoopDriver")!;
        var createMethod = typeof(DispatchProxy)
            .GetMethod(nameof(DispatchProxy.Create), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(imlType, typeof(KustoMainLoopProxy));

        var proxy = (KustoMainLoopProxy)createMethod.Invoke(null, null)!;
        proxy._driver = driver;
        proxy._terminal = terminal;
        return proxy;
    }

    /// <summary>
    /// Signal the event loop to wake up immediately (thread-safe).
    /// Call from any thread to trigger a redraw cycle without waiting for poll timeout.
    /// </summary>
    internal void SignalWakeup()
    {
        if (_wakeupWriteFd <= 0) return;
        Span<byte> buf = stackalloc byte[1];
        buf[0] = 1;
        write(_wakeupWriteFd, buf, 1);
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        return targetMethod?.Name switch
        {
            "Setup" => DoSetup(args?[0] as MainLoop),
            "TearDown" => DoTearDown(),
            "Wakeup" => DoWakeup(),
            "EventsPending" => DoEventsPending(),
            "Iteration" => DoIteration(),
            _ => null
        };
    }

    private object? DoSetup(MainLoop? mainLoop)
    {
        _mainLoop = mainLoop;
        if (_terminal != null)
            (_wakeupReadFd, _wakeupWriteFd) = _terminal.CreateWakeupPipe();
        return null;
    }

    private object? DoTearDown()
    {
        if (_wakeupReadFd > 0) close(_wakeupReadFd);
        if (_wakeupWriteFd > 0) close(_wakeupWriteFd);
        _wakeupReadFd = 0;
        _wakeupWriteFd = 0;
        return null;
    }

    private object? DoWakeup()
    {
        if (_wakeupWriteFd <= 0) return null;
        Span<byte> buf = stackalloc byte[1];
        buf[0] = 1;
        write(_wakeupWriteFd, buf, 1);
        return null;
    }

    private object DoEventsPending()
    {
        if (_terminal == null) return false;

        // Adaptive timeout: short when recently active, longer when idle
        long elapsed = Stopwatch.GetTimestamp() - _lastActivityTicks;
        int timeoutMs = elapsed < IdleAfterTicks ? ActivePollMs : IdlePollMs;

        Span<PollFd> fds = stackalloc PollFd[2];
        fds[0] = new PollFd { fd = _terminal.StdinFd, events = POLLIN };
        fds[1] = new PollFd { fd = _wakeupReadFd, events = POLLIN };

        int result = poll(fds, 2, timeoutMs);

        if (result <= 0) return false;

        // Drain wakeup pipe if signaled — briefly enter active mode for responsive redraws
        if ((fds[1].revents & POLLIN) != 0)
        {
            Span<byte> drain = stackalloc byte[64];
            read(_wakeupReadFd, drain, 64);
            _lastActivityTicks = Stopwatch.GetTimestamp();
        }

        return (fds[0].revents & POLLIN) != 0;
    }

    private object? DoIteration()
    {
        if (_terminal == null || _driver == null) return null;

        int bytesRead = _terminal.Read(_inputBuffer);
        if (bytesRead <= 0) return null;

        // Input received — stay in active (low-latency) polling mode
        _lastActivityTicks = Stopwatch.GetTimestamp();

        var sequences = new AnsiSequenceReader.ParsedSequence[64];
        int seqCount = _sequenceReader.Parse(_inputBuffer.AsSpan(0, bytesRead), sequences);

        for (int i = 0; i < seqCount; i++)
        {
            ref var seq = ref sequences[i];

            // Try mouse first
            var mouse = InputParser.ToMouse(ref seq);
            if (mouse != null)
            {
                _driver.RaiseMouseEvent(mouse);
                continue;
            }

            // Try key
            var key = InputParser.ToKey(ref seq);
            if (key != null && key != Key.Empty)
            {
                _driver.RaiseKeyDown(key);
                _driver.RaiseKeyUp(key);
            }
        }

        // Flush pending escape timeout
        if (_sequenceReader.FlushPendingEscape(out var escSeq))
        {
            var key = InputParser.ToKey(ref escSeq);
            if (key != null && key != Key.Empty)
            {
                _driver.RaiseKeyDown(key);
                _driver.RaiseKeyUp(key);
            }
        }

        return null;
    }
}
