using System;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Configuration;
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
            // This will load from embedded resources and user config files
            ConfigurationManager.Enable(ConfigLocations.All);
            ConfigurationManager.Apply();

            // Select driver: use high-performance Kusto driver with fallback
            var driverName = args.FirstOrDefault(a => a.StartsWith("--driver="))?.Split('=')[1];
            InitializeDriver(driverName);

            try
            {
                // Start the main window
                using var window = MainWindow.Create(connectionManager, userSettingsManager);
                Application.Run(window);
            }
            finally
            {
                Application.Shutdown();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.ToString()}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    static void InitializeDriver(string? driverName)
    {
        if (driverName is not null && driverName != "kusto")
        {
            // Explicit driver requested (e.g., --driver=ansi or --driver=dotnet)
            Application.Init(driverName: driverName);
            return;
        }

        // Try high-performance Kusto driver first
        try
        {
            var kustoDriver = new KustoConsoleDriver();
            Application.Init(driver: kustoDriver);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: KustoDriver failed ({ex.Message}), falling back to built-in driver.");
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
