using System;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using KustoTerminal.Core.Services;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.UI;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Threading;

namespace KustoTerminal.CLI
{
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

                // Initialize Terminal.Gui
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Application.Init();
                }
                else
                {
                    Application.Init(driverName: "NetDriver");
                }

                try
                {
                    // Start the main window
                    using var window = MainWindow.Run(connectionManager, userSettingsManager);
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

        static void SetDateTimeFormatting()
        {
            var customCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            customCulture.DateTimeFormat.ShortDatePattern = "dd-MM-yyyy";
            customCulture.DateTimeFormat.LongTimePattern = "HH:mm:ss";
            Thread.CurrentThread.CurrentCulture = customCulture;
            Thread.CurrentThread.CurrentUICulture = customCulture; 
        }
    }
}
