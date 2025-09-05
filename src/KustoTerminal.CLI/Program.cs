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
    }
}
