using System;
using System.Threading.Tasks;
using Terminal.Gui;
using KustoTerminal.Core.Services;
using KustoTerminal.Auth;
using KustoTerminal.UI;

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

                // Load existing connections
                await connectionManager.LoadConnectionsAsync();

                // Check Azure CLI authentication
                if (!authProvider.IsAuthenticated())
                {
                    Console.WriteLine("Azure CLI authentication not detected.");
                    Console.WriteLine("Please run 'az login' first, then restart the application.");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine("Azure CLI authentication detected.");
                
                // Validate authentication
                var isValidAuth = await authProvider.ValidateAuthenticationAsync();
                if (!isValidAuth)
                {
                    Console.WriteLine("Failed to validate Azure authentication.");
                    Console.WriteLine("Please run 'az login' again, then restart the application.");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine("Authentication validated successfully.");
                Console.WriteLine("Starting Terminal UI...");

                // Small delay to let user read the messages
                await Task.Delay(1000);

                // Initialize Terminal.Gui
                Application.Init();
                
                // Set up black background color scheme
                SetupBlackColorScheme();
                
                try
                {
                    // Start the main window
                    MainWindow.Run(connectionManager, authProvider);
                }
                finally
                {
                    Application.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        private static void SetupBlackColorScheme()
        {
            // Create a custom color scheme with black background
            var blackScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
                HotNormal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.BrightYellow, Color.Black),
                Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
            };

            // Apply the color scheme to all built-in color schemes
            Colors.TopLevel = blackScheme;
            Colors.Base = blackScheme;
            Colors.Dialog = blackScheme;
            Colors.Menu = blackScheme;
            Colors.Error = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.BrightRed, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.BrightRed, Color.Black),
                HotNormal = new Terminal.Gui.Attribute(Color.BrightRed, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.BrightRed, Color.Black),
                Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
            };
        }
    }
}
