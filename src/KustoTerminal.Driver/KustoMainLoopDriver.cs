using System.Reflection;
using Terminal.Gui.App;

namespace KustoTerminal.Driver;

/// <summary>
/// IMainLoopDriver implementation for the Kusto driver.
/// IMainLoopDriver is internal to Terminal.Gui, so we implement it via DispatchProxy.
///
/// This main loop processes input from the driver's input queue and handles
/// the main iteration cycle (input processing, layout, rendering).
/// </summary>
internal sealed class KustoMainLoopDriver : DispatchProxy
{
    private KustoConsoleDriver? _driver;
    private MainLoop? _mainLoop;

    // Static factory: DispatchProxy requires parameterless ctor
    internal static object Create(KustoConsoleDriver driver)
    {
        var iMainLoopDriverType = typeof(MainLoop).Assembly
            .GetType("Terminal.Gui.App.IMainLoopDriver")!;

        // Create a DispatchProxy that implements IMainLoopDriver
        var createMethod = typeof(DispatchProxy)
            .GetMethod(nameof(DispatchProxy.Create), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(iMainLoopDriverType, typeof(KustoMainLoopDriver));

        var proxy = (KustoMainLoopDriver)createMethod.Invoke(null, null)!;
        proxy._driver = driver;
        return proxy;
    }

    internal KustoMainLoopDriver(KustoConsoleDriver driver)
    {
        _driver = driver;
    }

    // Required by DispatchProxy
    public KustoMainLoopDriver()
    {
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        return targetMethod?.Name switch
        {
            "Setup" => Setup(args?[0] as MainLoop),
            "EventsPending" => EventsPending(),
            "Iteration" => Iteration(),
            "Wakeup" => Wakeup(),
            "TearDown" => TearDown(),
            _ => null
        };
    }

    private object? Setup(MainLoop? mainLoop)
    {
        _mainLoop = mainLoop;
        return null;
    }

    private object EventsPending()
    {
        // Brief sleep to avoid busy-spinning the main loop
        Thread.Sleep(1);
        return true; // Always return true; let Iteration decide what to do
    }

    private object? Iteration()
    {
        // Process input is handled by the driver
        return null;
    }

    private object? Wakeup()
    {
        // Nothing special needed — our main loop is already responsive
        return null;
    }

    private object? TearDown()
    {
        return null;
    }
}
