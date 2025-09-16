using System;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using KustoTerminal.Core.Services;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Auth;
using KustoTerminal.UI;
using System.Runtime.InteropServices;

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
                var authProvider = new AzureCliAuthenticationProvider();
                var userSettingsManager = new UserSettingsManager();

                // Load existing connections
                await connectionManager.LoadConnectionsAsync();
                var connections = await connectionManager.GetConnectionsAsync();
                
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
                    using var window = MainWindow.Run(connectionManager, authProvider, userSettingsManager);
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
    }
}
