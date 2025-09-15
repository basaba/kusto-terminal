using System;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;
using KustoTerminal.UI.Panes;
using KustoTerminal.UI.Dialogs;
using Terminal.Gui.Input;

namespace KustoTerminal.UI
{
    public class MainWindow : Window
    {
        private readonly IConnectionManager _connectionManager;
        private readonly IAuthenticationProvider _authProvider;
        
        private ConnectionPane _connectionPane;
        private QueryEditorPane _queryEditorPane;
        private ResultsPane _resultsPane;        
        private FrameView _leftFrame;
        private FrameView _rightFrame;
        private FrameView _bottomFrame;
        
        // Pane navigation
        private BasePane[] _navigablePanes;
        private int _currentPaneIndex = 0;
        
        // Query cancellation
        private CancellationTokenSource? _queryCancellationTokenSource;
        private Core.Services.KustoClient? _currentKustoClient;

        public MainWindow(IConnectionManager connectionManager, IAuthenticationProvider authProvider)
        {
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _authProvider = authProvider ?? throw new ArgumentNullException(nameof(authProvider));

            Title = "Kusto Terminal";
            X = 0;
            Y = 0; // Leave space for menu bar
            Width = Dim.Fill();
            Height = Dim.Fill() - 1; // Leave space for status bar

            InitializeComponents();
            SetupLayout();

            KeyDown += (o, key) =>
            {
                if (key.IsCtrl && (key.KeyCode & Key.Q.KeyCode) != 0
                || (key.IsCtrl && (key.KeyCode & Key.C.KeyCode) != 0))
                {
                    Application.Shutdown();
                    key.Handled = true;
                }
                else if (key == Key.Tab)
                {

                }
                else if (key == Key.Esc)
                {
                    key.Handled = true;
                }
            };
            // SetupKeyBindings();
            // TabStop = TabBehavior.TabGroup;
        }

        private void InitializeComponents()
        {
            // Create frames for layout
            _leftFrame = new FrameView()
            {
                Title = "Connections",
                X = 0,
                Y = 0,
                Width = 30,
                Height = Dim.Fill(),
                TabStop = TabBehavior.TabStop
            };

            _rightFrame = new FrameView()
            {
                Title = "Query Editor",
                X = 31,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Percent(60),
                TabStop = TabBehavior.TabStop
            };

            _bottomFrame = new FrameView()
            {
                Title = "Results",
                X = 31,
                Y = Pos.Bottom(_rightFrame),
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                TabStop = TabBehavior.TabStop
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
            // _navigablePanes = new BasePane[] { _connectionPane, _queryEditorPane, _resultsPane };

            // Set initial focus to connection pane
            _connectionPane.SetFocus();

            // Initialize frame border colors and highlighting
            UpdateFrameBorderColors();
            SetupPaneEventHandlers();

            // Set up events
            _connectionPane.ConnectionSelected += OnConnectionSelected;
            _queryEditorPane.QueryExecuteRequested += OnQueryExecuteRequested;
            _queryEditorPane.EscapePressed += OnQueryEditorEscapePressed;
            _queryEditorPane.QueryCancelRequested += OnQueryCancelRequested;
        }

        private void SetupPaneEventHandlers()
        {
            // Subscribe to focus change events from each pane
            // _connectionPane.FocusChanged += OnPaneFocusChanged;
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

        // private void SetupKeyBindings()
        // {
        //     // Focus handling for tab navigation
        //     CanFocus = true;
            
        //     // Global key bindings
        //     KeyPress += OnKeyPress;
        // }
        
        // private void OnKeyPress(KeyEventEventArgs args)
        // {
        //     // Handle TAB for pane navigation
        //     if (args.KeyEvent.Key == Key.Tab)
        //     {
        //         SwitchToNextPane();
        //         args.Handled = true;
        //         return;
        //     }
            
        //     // Handle Shift+TAB for reverse pane navigation
        //     if (args.KeyEvent.Key == (Key.ShiftMask | Key.Tab))
        //     {
        //         SwitchToPreviousPane();
        //         args.Handled = true;
        //         return;
        //     }
            
        //     // Handle Ctrl+E for edit connection
        //     if (args.KeyEvent.Key == (Key.CtrlMask | Key.E))
        //     {
        //         EditConnection();
        //         args.Handled = true;
        //     }
        //     // Handle Ctrl+N for new connection
        //     else if (args.KeyEvent.Key == (Key.CtrlMask | Key.N))
        //     {
        //         NewConnection();
        //         args.Handled = true;
        //     }
        //     // Handle Ctrl+L for clear query
        //     else if (args.KeyEvent.Key == (Key.CtrlMask | Key.L))
        //     {
        //         ClearQuery();
        //         args.Handled = true;
        //     }
        //     // Handle Ctrl+S for export results
        //     else if (args.KeyEvent.Key == (Key.CtrlMask | Key.S))
        //     {
        //         ExportResults();
        //         args.Handled = true;
        //     }
        //     // Handle Delete key for delete connection
        //     else if (args.KeyEvent.Key == Key.DeleteChar)
        //     {
        //         DeleteConnection();
        //         args.Handled = true;
        //     }
        // }

        // private void SwitchToNextPane()
        // {
        //     _currentPaneIndex = (_currentPaneIndex + 1) % _navigablePanes.Length;
        //     SetFocusToCurrentPane();
        // }

        // private void SwitchToPreviousPane()
        // {
        //     _currentPaneIndex = (_currentPaneIndex - 1 + _navigablePanes.Length) % _navigablePanes.Length;
        //     SetFocusToCurrentPane();
        // }

        // private void SetFocusToCurrentPane()
        // {
        //     var currentPane = _navigablePanes[_currentPaneIndex];
        //     currentPane.SetFocus();
            
        //     // The pane highlighting will be handled by the OnPaneFocusChanged event
        //     // But we still need to update frame titles and borders
        //     UpdateFrameTitles();
        //     UpdateFrameBorderColors();
        // }

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
            // var normalColorScheme = ColorSchemeFactory.CreateStandard();
            // var activeColorScheme = ColorSchemeFactory.CreateActiveFrame();

            // Reset all frames to normal color
            // _leftFrame.ColorScheme = normalColorScheme;
            // _rightFrame.ColorScheme = normalColorScheme;
            // _bottomFrame.ColorScheme = normalColorScheme;

            // Highlight the active frame border with a different color than the pane content
            // This creates a layered highlighting effect: yellow frame + cyan pane content
            // switch (_currentPaneIndex)
            // {
            //     case 0: // Connection pane
            //         _leftFrame.ColorScheme = activeColorScheme;
            //         break;
            //     case 1: // Query editor pane
            //         _rightFrame.ColorScheme = activeColorScheme;
            //         break;
            //     case 2: // Results pane
            //         _bottomFrame.ColorScheme = activeColorScheme;
            //         break;
            // }

            // // Force redraw of all frames
            // _leftFrame.SetNeedsDisplay();
            // _rightFrame.SetNeedsDisplay();
            // _bottomFrame.SetNeedsDisplay();
        }

        private void OnConnectionSelected(object? sender, KustoConnection connection)
        {
            _queryEditorPane.SetConnection(connection);
            UpdateStatusBar($"Connected to: {connection.DisplayName}");
            
            _currentPaneIndex = 1; // Query editor is at index 1
            _queryEditorPane.FocusEditor();
        }

        private async void OnQueryExecuteRequested(object? sender, string query)
        {
            await ExecuteQueryAsync(query);
        }

        private void OnQueryEditorEscapePressed(object? sender, EventArgs e)
        {
            _resultsPane.SetFocus();
        }

        private async void OnQueryCancelRequested(object? sender, EventArgs e)
        {
            // Cancel the current query if one is running
            if (_queryCancellationTokenSource != null && !_queryCancellationTokenSource.Token.IsCancellationRequested)
            {
                UpdateStatusBar("Query cancellation requested...");
                
                // First cancel the token to stop any local processing
                _queryCancellationTokenSource.Cancel();
                
                // Then try to cancel on the server side using the Kusto client
                if (_currentKustoClient != null)
                {
                    try
                    {
                        await _currentKustoClient.CancelCurrentQueryAsync();
                    }
                    catch (Exception ex)
                    {
                        // Log but don't throw - cancellation errors shouldn't crash the UI
                        UpdateStatusBar($"Warning: Could not cancel query on server: {ex.Message}");
                    }
                }
            }
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
                    Application.Invoke(() => _connectionPane.RefreshConnections());
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
                    Application.Invoke(() => _connectionPane.RefreshConnections());
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
                    Application.Invoke(() => _connectionPane.RefreshConnections());
                });
            }
        }

        private void ExportResults()
        {
            // Focus on results pane to trigger export
            _currentPaneIndex = 2; // Results pane
            // SetFocusToCurrentPane();
            // The actual export will be handled by the ResultsPane's Ctrl+S handler
        }

        private async Task ExecuteQueryAsync(string query)
        {
            // Cancel any existing query
            _queryCancellationTokenSource?.Cancel();
            _queryCancellationTokenSource?.Dispose();
            
            // Create new cancellation token source for this query
            _queryCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _queryCancellationTokenSource.Token;
            
            try
            {
                var connection = _connectionPane.GetSelectedConnection();
                if (connection == null)
                {
                    UpdateStatusBar("No connection selected");
                    Application.Invoke(() => _queryEditorPane.SetExecuting(false));
                    return;
                }

                UpdateStatusBar("Executing query...");
                
                // Create progress handler
                var progress = new Progress<string>(message =>
                {
                    Application.Invoke(() =>
                    {
                        UpdateStatusBar(message);
                        _queryEditorPane.UpdateProgressMessage(message);
                    });
                });
                
                _currentKustoClient = new Core.Services.KustoClient(connection, _authProvider);
                var result = await _currentKustoClient.ExecuteQueryAsync(query, cancellationToken, progress);
                _currentKustoClient.Dispose();
                _currentKustoClient = null;
                
                Application.Invoke(() =>
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
            catch (OperationCanceledException)
            {
                Application.Invoke(() =>
                {
                    _queryEditorPane.SetExecuting(false);
                    UpdateStatusBar("Query was cancelled");
                });
            }
            catch (Exception ex)
            {
                Application.Invoke(() =>
                {
                    _queryEditorPane.SetExecuting(false);
                    UpdateStatusBar($"Error: {ex.Message}");
                });
            }
            finally
            {
                // Clean up the cancellation token source and client
                if (_queryCancellationTokenSource != null)
                {
                    _queryCancellationTokenSource.Dispose();
                    _queryCancellationTokenSource = null;
                }
                
                if (_currentKustoClient != null)
                {
                    _currentKustoClient.Dispose();
                    _currentKustoClient = null;
                }
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
Esc          - Cancel running query / Switch to results pane
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
            Application.Invoke(() =>
            {
                //_statusBar.SetMessage(message);
                // Terminal.Gui will handle status updates
                // Title = $"Kusto Terminal - {message}";
            });
        }

        public static void Run(IConnectionManager connectionManager, IAuthenticationProvider authProvider)
        {
            //var top = Application.Top;
            
            var mainWindow = new MainWindow(connectionManager, authProvider);
            //top.Add(mainWindow);
            
            Application.Run(mainWindow);
        }
    }
}