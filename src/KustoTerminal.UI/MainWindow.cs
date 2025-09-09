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
        
        // Pane navigation
        private BasePane[] _navigablePanes;
        private int _currentPaneIndex = 0;

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
                    new MenuItem("_Paste", "Paste text", Paste),
                    new MenuItem("_Edit Connection", "Edit selected connection (Ctrl+E)", EditConnection)
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
                new StatusItem(Key.CtrlMask | Key.L, "~Ctrl+L~ Clear", ClearQuery),
                new StatusItem(Key.CtrlMask | Key.N, "~Ctrl+N~ New", NewConnection),
                new StatusItem(Key.CtrlMask | Key.E, "~Ctrl+E~ Edit", EditConnection),
                new StatusItem(Key.DeleteChar, "~Del~ Delete", DeleteConnection),
                new StatusItem(Key.CtrlMask | Key.S, "~Ctrl+S~ Export", ExportResults),
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

            // Set up navigable panes array for TAB navigation
            _navigablePanes = new BasePane[] { _connectionPane, _queryEditorPane, _resultsPane };

            // Set initial focus to connection pane
            _connectionPane.SetFocus();

            // Initialize frame border colors and highlighting
            UpdateFrameBorderColors();
            SetupPaneEventHandlers();

            // Set up events
            _connectionPane.ConnectionSelected += OnConnectionSelected;
            _queryEditorPane.QueryExecuteRequested += OnQueryExecuteRequested;
            _queryEditorPane.EscapePressed += OnQueryEditorEscapePressed;
        }

        private void SetupPaneEventHandlers()
        {
            // Subscribe to focus change events from each pane
            _connectionPane.FocusChanged += OnPaneFocusChanged;
            _queryEditorPane.FocusChanged += OnPaneFocusChanged;
            _resultsPane.FocusChanged += OnPaneFocusChanged;
        }

        private void OnPaneFocusChanged(object? sender, bool hasFocus)
        {
            if (sender is BasePane pane && hasFocus)
            {
                // Update current pane index when a pane receives focus
                for (int i = 0; i < _navigablePanes.Length; i++)
                {
                    if (_navigablePanes[i] == pane)
                    {
                        _currentPaneIndex = i;
                        break;
                    }
                }
                
                // Update frame highlighting
                UpdateFrameTitles();
                UpdateFrameBorderColors();
            }
        }

        private void SetupKeyBindings()
        {
            // Focus handling for tab navigation
            CanFocus = true;
            
            // Global key bindings
            KeyPress += OnKeyPress;
        }
        
        private void OnKeyPress(KeyEventEventArgs args)
        {
            // Handle TAB for pane navigation
            if (args.KeyEvent.Key == Key.Tab)
            {
                SwitchToNextPane();
                args.Handled = true;
                return;
            }
            
            // Handle Shift+TAB for reverse pane navigation
            if (args.KeyEvent.Key == (Key.ShiftMask | Key.Tab))
            {
                SwitchToPreviousPane();
                args.Handled = true;
                return;
            }
            
            // Handle Ctrl+E for edit connection
            if (args.KeyEvent.Key == (Key.CtrlMask | Key.E))
            {
                EditConnection();
                args.Handled = true;
            }
            // Handle Ctrl+N for new connection
            else if (args.KeyEvent.Key == (Key.CtrlMask | Key.N))
            {
                NewConnection();
                args.Handled = true;
            }
            // Handle Ctrl+L for clear query
            else if (args.KeyEvent.Key == (Key.CtrlMask | Key.L))
            {
                ClearQuery();
                args.Handled = true;
            }
            // Handle Ctrl+S for export results
            else if (args.KeyEvent.Key == (Key.CtrlMask | Key.S))
            {
                ExportResults();
                args.Handled = true;
            }
            // Handle Delete key for delete connection
            else if (args.KeyEvent.Key == Key.DeleteChar)
            {
                DeleteConnection();
                args.Handled = true;
            }
        }

        private void SwitchToNextPane()
        {
            _currentPaneIndex = (_currentPaneIndex + 1) % _navigablePanes.Length;
            SetFocusToCurrentPane();
        }

        private void SwitchToPreviousPane()
        {
            _currentPaneIndex = (_currentPaneIndex - 1 + _navigablePanes.Length) % _navigablePanes.Length;
            SetFocusToCurrentPane();
        }

        private void SetFocusToCurrentPane()
        {
            var currentPane = _navigablePanes[_currentPaneIndex];
            currentPane.SetFocus();
            
            // The pane highlighting will be handled by the OnPaneFocusChanged event
            // But we still need to update frame titles and borders
            UpdateFrameTitles();
            UpdateFrameBorderColors();
        }

        private void UpdateFrameTitles()
        {
            // Reset all frame titles
            _leftFrame.Title = "Connections";
            _rightFrame.Title = "Query Editor";
            _bottomFrame.Title = "Results";
            
            // Highlight the active frame title
            switch (_currentPaneIndex)
            {
                case 0: // Connection pane
                    _leftFrame.Title = "▶ Connections";
                    break;
                case 1: // Query editor pane
                    _rightFrame.Title = "▶ Query Editor";
                    break;
                case 2: // Results pane
                    _bottomFrame.Title = "▶ Results";
                    break;
            }
        }

        private void UpdateFrameBorderColors()
        {
            // Use centralized color scheme factory
            var normalColorScheme = ColorSchemeFactory.CreateStandard();
            var activeColorScheme = ColorSchemeFactory.CreateActiveFrame();

            // Reset all frames to normal color
            _leftFrame.ColorScheme = normalColorScheme;
            _rightFrame.ColorScheme = normalColorScheme;
            _bottomFrame.ColorScheme = normalColorScheme;

            // Highlight the active frame border with a different color than the pane content
            // This creates a layered highlighting effect: yellow frame + cyan pane content
            switch (_currentPaneIndex)
            {
                case 0: // Connection pane
                    _leftFrame.ColorScheme = activeColorScheme;
                    break;
                case 1: // Query editor pane
                    _rightFrame.ColorScheme = activeColorScheme;
                    break;
                case 2: // Results pane
                    _bottomFrame.ColorScheme = activeColorScheme;
                    break;
            }

            // Force redraw of all frames
            _leftFrame.SetNeedsDisplay();
            _rightFrame.SetNeedsDisplay();
            _bottomFrame.SetNeedsDisplay();
        }

        private void OnConnectionSelected(object? sender, KustoConnection connection)
        {
            _queryEditorPane.SetConnection(connection);
            UpdateStatusBar($"Connected to: {connection.DisplayName}");
            
            // Switch focus to query editor pane when connection is selected
            _currentPaneIndex = 1; // Query editor is at index 1
            SetFocusToCurrentPane();
            _queryEditorPane.FocusEditor();
        }

        private async void OnQueryExecuteRequested(object? sender, string query)
        {
            await ExecuteQueryAsync(query);
        }

        private void OnQueryEditorEscapePressed(object? sender, EventArgs e)
        {
            // Switch focus to results pane when ESC is pressed in query editor
            _currentPaneIndex = 2; // Results pane is at index 2
            SetFocusToCurrentPane();
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

        private void EditConnection()
        {
            var selectedConnection = _connectionPane.GetSelectedConnection();
            if (selectedConnection == null)
            {
                MessageBox.ErrorQuery("Error", "No connection selected. Please select a connection to edit.", "OK");
                return;
            }

            var dialog = new ConnectionDialog(selectedConnection);
            Application.Run(dialog);

            if (dialog.Result != null)
            {
                Task.Run(async () =>
                {
                    await _connectionManager.UpdateConnectionAsync(dialog.Result);
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

        private void ClearQuery()
        {
            _queryEditorPane.FocusEditor();
            // The actual clear will be handled by the QueryEditorPane's Ctrl+L handler
        }

        private void DeleteConnection()
        {
            var selectedConnection = _connectionPane.GetSelectedConnection();
            if (selectedConnection == null)
            {
                MessageBox.ErrorQuery("Error", "No connection selected. Please select a connection to delete.", "OK");
                return;
            }

            var result = MessageBox.Query("Confirm Delete",
                $"Are you sure you want to delete connection '{selectedConnection.DisplayName}'?",
                "Yes", "No");

            if (result == 0) // Yes
            {
                Task.Run(async () =>
                {
                    await _connectionManager.DeleteConnectionAsync(selectedConnection.Id);
                    Application.MainLoop.Invoke(() => _connectionPane.RefreshConnections());
                });
            }
        }

        private void ExportResults()
        {
            // Focus on results pane to trigger export
            _currentPaneIndex = 2; // Results pane
            SetFocusToCurrentPane();
            // The actual export will be handled by the ResultsPane's Ctrl+S handler
        }

        private async Task ExecuteQueryAsync(string query)
        {
            try
            {
                var connection = _connectionPane.GetSelectedConnection();
                if (connection == null)
                {
                    UpdateStatusBar("No connection selected");
                    Application.MainLoop.Invoke(() => _queryEditorPane.SetExecuting(false));
                    return;
                }

                UpdateStatusBar("Executing query...");
                
                // Create progress handler
                var progress = new Progress<string>(message =>
                {
                    Application.MainLoop.Invoke(() =>
                    {
                        UpdateStatusBar(message);
                        _queryEditorPane.UpdateProgressMessage(message);
                    });
                });
                
                var client = new Core.Services.KustoClient(connection, _authProvider);
                var result = await client.ExecuteQueryAsync(query, progress: progress);
                client.Dispose();
                
                Application.MainLoop.Invoke(() =>
                {
                    _queryEditorPane.SetExecuting(false);
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
                    _queryEditorPane.SetExecuting(false);
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

Query Editor:
F5           - Execute query
Ctrl+L       - Clear query
Ctrl+A       - Select all text

Connections:
Ctrl+N       - Add new connection
Ctrl+E       - Edit selected connection
Del          - Delete selected connection
Enter        - Connect to selected connection

Results:
Ctrl+S       - Export results

Dialog Navigation:
Enter        - Accept/OK
Esc          - Cancel

General:
Tab          - Switch to next pane
Shift+Tab    - Switch to previous pane
Ctrl+C       - Copy
Ctrl+V       - Paste
Ctrl+Q       - Quit application
F1           - Show this help";

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