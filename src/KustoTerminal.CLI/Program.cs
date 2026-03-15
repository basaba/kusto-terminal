using System;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drivers;
using KustoTerminal.Core.Services;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.UI;
using KustoTerminal.Driver;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Threading;

namespace KustoTerminal.CLI;

class Program
{
    private static string? s_fpsLogPath;
    private static KustoConsoleDriver? s_kustoDriver;

    static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("Kusto Terminal - Azure Data Explorer Client");
            Console.WriteLine("Initializing...");

            // Initialize services
            var connectionManager = new ConnectionManager();
            var userSettingsManager = new UserSettingsManager();

            // Load existing connections
            await connectionManager.LoadConnectionsAsync();
            var connections = await connectionManager.GetConnectionsAsync();

            SetDateTimeFormatting();

            Console.WriteLine("Starting Terminal UI...");

            // Enable ConfigurationManager to load color schemes and themes
            ConfigurationManager.Enable(ConfigLocations.All);
            ConfigurationManager.Apply();

            // Parse --driver flag: kusto (default), net (fallback)
            var driverArg = args.FirstOrDefault(a => a.StartsWith("--driver="));
            var driverName = driverArg?.Split('=', 2).ElementAtOrDefault(1) ?? "kusto";
            bool showFps = args.Contains("--fps");

            if (showFps)
            {
                s_fpsLogPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".kusto-terminal-fps.log");
                // Write stats on process exit (handles SIGTERM, normal exit, etc.)
                AppDomain.CurrentDomain.ProcessExit += (_, _) => WriteFpsLog();
            }

            InitDriver(driverName, showFps);

            try
            {
                // Start the main window
                using var window = MainWindow.Create(connectionManager, userSettingsManager);
                Application.Run(window);
            }
            finally
            {
                // Grab driver reference BEFORE Shutdown disposes it
                s_kustoDriver ??= Application.Driver as KustoConsoleDriver;
                Application.Shutdown();
                WriteFpsLog();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.ToString()}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    static void WriteFpsLog()
    {
        if (s_fpsLogPath == null) return;
        try
        {
            var summary = s_kustoDriver?.GetRenderStatsSummary()
                ?? "No frames recorded (driver may have fallen back to NetDriver).\n";
            File.WriteAllText(s_fpsLogPath, summary);
        }
        catch { }
    }

    static void InitDriver(string driverName, bool showFps)
    {
        if (driverName == "net")
        {
            Application.Init(driverName: "NetDriver");
            return;
        }

        // Try the high-performance KustoConsoleDriver with auto-fallback
        try
        {
            var driver = new KustoConsoleDriver();
            if (showFps)
                driver.EnableRenderStats();
            Application.Init(driver: driver);
            s_kustoDriver = driver;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"KustoConsoleDriver init failed: {ex.Message}");
            Console.Error.WriteLine("Falling back to NetDriver...");
            Application.Init(driverName: "NetDriver");
        }
    }

    static void SetDateTimeFormatting()
    {
        var customCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        customCulture.DateTimeFormat.ShortDatePattern = "yyyy-MM-dd";
        customCulture.DateTimeFormat.LongTimePattern = "HH:mm:ss";
        Thread.CurrentThread.CurrentCulture = customCulture;
        Thread.CurrentThread.CurrentUICulture = customCulture; 
    }
}
