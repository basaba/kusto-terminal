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
                
                // Check if there are any authenticated connections that require Azure CLI
                var hasAuthenticatedConnections = connections.Any(c => c.AuthType == Core.Models.AuthenticationType.AzureCli);
                
                if (hasAuthenticatedConnections)
                {
                    // Check Azure CLI authentication only if there are connections that need it
                    if (!authProvider.IsAuthenticated())
                    {
                        Console.WriteLine("Azure CLI authentication not detected.");
                        Console.WriteLine("Note: You can still use unauthenticated connections.");
                        Console.WriteLine("For authenticated connections, please run 'az login' first.");
                    }
                    else
                    {
                        Console.WriteLine("Azure CLI authentication detected.");
                        
                        // Validate authentication
                        var isValidAuth = await authProvider.ValidateAuthenticationAsync();
                        if (!isValidAuth)
                        {
                            Console.WriteLine("Failed to validate Azure authentication.");
                            Console.WriteLine("Note: You can still use unauthenticated connections.");
                            Console.WriteLine("For authenticated connections, please run 'az login' again.");
                        }
                        else
                        {
                            Console.WriteLine("Authentication validated successfully.");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No authenticated connections configured. Azure CLI authentication not required.");
                }
                Console.WriteLine("Starting Terminal UI...");

                // Small delay to let user read the messages
                await Task.Delay(1000);

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

        // private static void SetupBlackColorScheme()
        // {
        //     // Create a custom color scheme with black background
        //     var blackScheme = new ColorScheme()
        //     {
        //         Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
        //         Focus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
        //         HotNormal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
        //         HotFocus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
        //         Disabled = new Terminal.Gui.Attribute(Color.Black, Color.Black)
        //     };

        //     // Apply the color scheme to all built-in color schemes
        //     Colors.TopLevel = blackScheme;
        //     Colors.Base = blackScheme;
        //     Colors.Dialog = blackScheme;
        //     Colors.Menu = blackScheme;
        //     Colors.Error = new ColorScheme()
        //     {
        //         Normal = new Terminal.Gui.Attribute(Color.BrightRed, Color.Black),
        //         Focus = new Terminal.Gui.Attribute(Color.BrightRed, Color.Black),
        //         HotNormal = new Terminal.Gui.Attribute(Color.BrightRed, Color.Black),
        //         HotFocus = new Terminal.Gui.Attribute(Color.BrightRed, Color.Black),
        //         Disabled = new Terminal.Gui.Attribute(Color.Black, Color.Black)
        //     };
        // }
    }
}
