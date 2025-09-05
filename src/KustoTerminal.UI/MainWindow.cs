using System;
using System.Threading.Tasks;
using Terminal.Gui;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;
using KustoTerminal.UI.Panes;
using KustoTerminal.UI.Dialogs;

namespace KustoTerminal.UI
{
    public class MainWindow : Window
    {
        private readonly IConnectionManager _connectionManager;
        private readonly IAuthenticationProvider _authProvider;
        
        private ConnectionPane _connectionPane;
        private QueryEditorPane _queryEditorPane;
        private ResultsPane _resultsPane;
        private StatusBar _statusBar;
        
        private MenuBar _menuBar;
        private FrameView _leftFrame;
        private FrameView _rightFrame;
        private FrameView _bottomFrame;

        public MainWindow(IConnectionManager connectionManager, IAuthenticationProvider authProvider)
        {
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _authProvider = authProvider ?? throw new ArgumentNullException(nameof(authProvider));
            
            Title = "Kusto Terminal - Azure Data Explorer Client";
            X = 0;
            Y = 1; // Leave space for menu bar
            Width = Dim.Fill();
            Height = Dim.Fill() - 1; // Leave space for status bar
            
            InitializeComponents();
            SetupLayout();
            SetupKeyBindings();
        }

        private void InitializeComponents()
        {
            // Create menu bar
            _menuBar = new MenuBar(new MenuBarItem[]
            {
                new MenuBarItem("_File", new MenuItem[]
                {
                    new MenuItem("_New Connection", "Add a new Kusto connection", NewConnection),
                    new MenuItem("_Exit", "Exit the application", () => Application.RequestStop())
                }),
                new MenuBarItem("_Edit", new MenuItem[]
                {
                    new MenuItem("_Copy", "Copy selected text", Copy),
                    new MenuItem("_Paste", "Paste text", Paste)
                }),
                new MenuBarItem("_Query", new MenuItem[]
                {
                    new MenuItem("_Execute", "Execute current query (F5)", ExecuteQuery),
                    new MenuItem("_Clear Results", "Clear query results", ClearResults)
                }),
                new MenuBarItem("_Help", new MenuItem[]
                {
                    new MenuItem("_About", "About Kusto Terminal", ShowAbout)
                })
            });

            // Create status bar
            _statusBar = new StatusBar(new StatusItem[]
            {
                new StatusItem(Key.F5, "~F5~ Execute", ExecuteQuery),
                new StatusItem(Key.F1, "~F1~ Help", ShowHelp),
                new StatusItem(Key.CtrlMask | Key.Q, "~Ctrl+Q~ Quit", () => Application.RequestStop())
            });

            // Create frames for layout
            _leftFrame = new FrameView("Connections")
            {
                X = 0,
                Y = 0,
                Width = 30,
                Height = Dim.Fill()
            };

            _rightFrame = new FrameView("Query Editor")
            {
                X = 31,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Percent(60)
            };

            _bottomFrame = new FrameView("Results")
            {
                X = 31,
                Y = Pos.Bottom(_rightFrame),
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            // Create panes
            _connectionPane = new ConnectionPane(_connectionManager)
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            _queryEditorPane = new QueryEditorPane()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            _resultsPane = new ResultsPane()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
        }

        private void SetupLayout()
        {
            // Add frames to window
            Add(_leftFrame, _rightFrame, _bottomFrame);

            // Add panes to frames
            _leftFrame.Add(_connectionPane);
            _rightFrame.Add(_queryEditorPane);
            _bottomFrame.Add(_resultsPane);

            // Set up events
            _connectionPane.ConnectionSelected += OnConnectionSelected;
            _queryEditorPane.QueryExecuteRequested += OnQueryExecuteRequested;
        }

        private void SetupKeyBindings()
        {
            // Focus handling for tab navigation
            CanFocus = true;
        }

        private void OnConnectionSelected(object? sender, KustoConnection connection)
        {
            _queryEditorPane.SetConnection(connection);
            UpdateStatusBar($"Connected to: {connection.DisplayName}");
        }

        private async void OnQueryExecuteRequested(object? sender, string query)
        {
            await ExecuteQueryAsync(query);
        }

        private void NewConnection()
        {
            var dialog = new ConnectionDialog();
            Application.Run(dialog);

            if (dialog.Result != null)
            {
                Task.Run(async () =>
                {
                    await _connectionManager.AddConnectionAsync(dialog.Result);
                    Application.MainLoop.Invoke(() => _connectionPane.RefreshConnections());
                });
            }
        }

        private void Copy()
        {
            if (MostFocused is TextView textView)
            {
                textView.Copy();
            }
        }

        private void Paste()
        {
            if (MostFocused is TextView textView)
            {
                textView.Paste();
            }
        }

        private void ExecuteQuery()
        {
            var query = _queryEditorPane.GetCurrentQuery();
            if (!string.IsNullOrWhiteSpace(query))
            {
                Task.Run(() => ExecuteQueryAsync(query));
            }
        }

        private async Task ExecuteQueryAsync(string query)
        {
            try
            {
                var connection = _connectionPane.GetSelectedConnection();
                if (connection == null)
                {
                    UpdateStatusBar("No connection selected");
                    return;
                }

                UpdateStatusBar("Executing query...");
                
                var client = new Core.Services.KustoClient(connection, _authProvider);
                var result = await client.ExecuteQueryAsync(query);
                client.Dispose();
                
                Application.MainLoop.Invoke(() =>
                {
                    _resultsPane.DisplayResult(result);
                    if (result.IsSuccess)
                    {
                        UpdateStatusBar($"Query executed successfully. {result.RowCount} rows returned in {result.Duration.TotalMilliseconds:F0}ms");
                    }
                    else
                    {
                        UpdateStatusBar($"Query failed: {result.ErrorMessage}");
                    }
                });
            }
            catch (Exception ex)
            {
                Application.MainLoop.Invoke(() =>
                {
                    UpdateStatusBar($"Error: {ex.Message}");
                });
            }
        }

        private void ClearResults()
        {
            _resultsPane.Clear();
            UpdateStatusBar("Results cleared");
        }

        private void ShowAbout()
        {
            MessageBox.Query("About", "Kusto Terminal v1.0\nAzure Data Explorer CLI Client\n\nInspired by k9s", "OK");
        }

        private void ShowHelp()
        {
            var helpText = @"Kusto Terminal - Keyboard Shortcuts

F5           - Execute query
Ctrl+N       - New connection
Ctrl+Q       - Quit application
Ctrl+C       - Copy
Ctrl+V       - Paste

Navigation:
Tab          - Switch between panes
Arrow Keys   - Navigate within panes
Enter        - Select/Activate item";

            MessageBox.Query("Help", helpText, "OK");
        }

        private void UpdateStatusBar(string message)
        {
            // Update status bar with current message
            Application.MainLoop.Invoke(() =>
            {
                // Terminal.Gui will handle status updates
            });
        }

        public static void Run(IConnectionManager connectionManager, IAuthenticationProvider authProvider)
        {
            var top = Application.Top;
            
            var mainWindow = new MainWindow(connectionManager, authProvider);
            top.Add(mainWindow._menuBar, mainWindow, mainWindow._statusBar);
            
            Application.Run();
        }
    }
}