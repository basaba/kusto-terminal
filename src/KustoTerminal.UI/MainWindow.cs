using System;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;
using KustoTerminal.Core.Services;
using KustoTerminal.UI.Panes;
using KustoTerminal.UI.Dialogs;
using Terminal.Gui.Input;
using Terminal.Gui.Drivers;

namespace KustoTerminal.UI
{
    public class MainWindow : Window
    {
        private readonly IConnectionManager _connectionManager;
        private readonly IAuthenticationProvider _authProvider;
        private readonly IUserSettingsManager _userSettingsManager;
        
        private ConnectionPane _connectionPane;
        private QueryEditorPane _queryEditorPane;
        private ResultsPane _resultsPane;        
        private FrameView _leftFrame;
        private FrameView _rightTopFrame;
        private FrameView _rightBottomFrame;

        // Query cancellation
        private CancellationTokenSource? _queryCancellationTokenSource;
        private Core.Services.KustoClient? _currentKustoClient;
        
        // Maximize state
        private bool _isQueryEditorMaximized = false;
        private bool _isResultsPaneMaximized = false;
        private Dim _originalRightFrameHeight;
        private Pos _originalBottomFrameY;
        private Dim _originalBottomFrameHeight;

        public MainWindow(IConnectionManager connectionManager, IUserSettingsManager userSettingsManager)
        {
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _userSettingsManager = userSettingsManager ?? throw new ArgumentNullException(nameof(userSettingsManager));

            Title = "Kusto Terminal - (Ctrl+Q to quit)";
            X = 0;
            Y = 0; // Leave space for menu bar
            Width = Dim.Fill();
            Height = Dim.Fill() - 1; // Leave space for status bar

            InitializeComponents();
            SetupLayout();
            SetKeyboard();
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

            _rightTopFrame = new FrameView()
            {
                Title = "Query Editor",
                X = 31,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Percent(60),
                TabStop = TabBehavior.TabStop
            };

            _rightBottomFrame = new FrameView()
            {
                Title = "Results",
                X = 31,
                Y = Pos.Bottom(_rightTopFrame),
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

            _queryEditorPane = new QueryEditorPane(_userSettingsManager)
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

            var _shortcutsLabel = new Label()
            {
                Text = "Ctrl+Q to quit",
                X = 1,
                Y = Pos.AnchorEnd(1),
                Width = Dim.Fill(),
                Height = 1
            };
        }

        private void SetupLayout()
        {
            // Add frames to window
            Add(_leftFrame, _rightTopFrame, _rightBottomFrame);

            // Add panes to frames
            _leftFrame.Add(_connectionPane);
            _rightTopFrame.Add(_queryEditorPane);
            _rightBottomFrame.Add(_resultsPane);
            
            // Set initial focus to connection pane
            _connectionPane.SetFocus();

            // Set up events
            _connectionPane.ConnectionSelected += OnConnectionSelected;
            _queryEditorPane.QueryExecuteRequested += OnQueryExecuteRequested;
            _queryEditorPane.EscapePressed += OnQueryEditorEscapePressed;
            _queryEditorPane.QueryCancelRequested += OnQueryCancelRequested;
            _queryEditorPane.MaximizeToggleRequested += OnQueryEditorMaximizeToggleRequested;
            _resultsPane.MaximizeToggleRequested += OnResultsPaneMaximizeToggleRequested;
            
            // Store original dimensions for restore
            _originalRightFrameHeight = _rightTopFrame.Height;
            _originalBottomFrameY = _rightBottomFrame.Y;
            _originalBottomFrameHeight = _rightBottomFrame.Height;
            
            // Load last query asynchronously
            LoadLastQueryAsync();
        }

        private void SetKeyboard()
        {
            KeyDown += (o, key) =>
            {
                if (key.KeyCode == (KeyCode.CtrlMask | Key.Q.KeyCode)
                || key.KeyCode == (KeyCode.CtrlMask | Key.C.KeyCode))
                {
                    Application.Shutdown();
                    key.Handled = true;
                }
                else if (key == Key.F12)
                {
                    ToggleMaximizeBasedOnFocus();
                    key.Handled = true;
                }
                else if (key == Key.Esc)
                {
                    key.Handled = true;
                }
                else if (key == (KeyCode.AltMask | Key.CursorRight.KeyCode))
                {
                    if (_leftFrame.HasFocus)
                    {
                        _rightTopFrame.SetFocus();
                        key.Handled = true;
                    }
                }
                else if (key == (KeyCode.AltMask | Key.CursorLeft.KeyCode))
                {
                    if (_rightBottomFrame.HasFocus || _rightTopFrame.HasFocus)
                    {
                        _leftFrame.SetFocus();
                    }
                }
                else if (key == (KeyCode.AltMask | Key.CursorDown.KeyCode))
                {
                    if (_rightTopFrame.HasFocus)
                    {
                        _rightBottomFrame.SetFocus();
                        key.Handled = true;
                    }
                }
                else if (key == (KeyCode.AltMask | Key.CursorUp.KeyCode))
                {
                    if (_rightBottomFrame.HasFocus)
                    {
                        _rightTopFrame.SetFocus();
                        key.Handled = true;
                    }
                }
            };
        }

        private void OnConnectionSelected(object? sender, KustoConnection connection)
        {
            _queryEditorPane.SetConnection(connection);
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

        private void OnQueryCancelRequested(object? sender, EventArgs e)
        {
            // Cancel the current query if one is running
            if (_queryCancellationTokenSource != null && !_queryCancellationTokenSource.Token.IsCancellationRequested)
            {
                // First cancel the token to stop any local processing
                _queryCancellationTokenSource.Cancel();
                
                // Immediately set executing to false to allow new queries
                Application.Invoke(() => _queryEditorPane.SetExecuting(false));
                
                // Then try to cancel on the server side using the Kusto client (fire-and-forget)
                if (_currentKustoClient != null)
                {
                    var clientToCancel = _currentKustoClient;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await clientToCancel.CancelCurrentQueryAsync();
                        }
                        catch (Exception ex)
                        {
                            // Log but don't throw - cancellation errors shouldn't crash the UI
                            Console.WriteLine($"Warning: Server-side cancellation failed: {ex.Message}");
                        }
                    });
                }
            }
        }

        private void OnQueryEditorMaximizeToggleRequested(object? sender, EventArgs e)
        {
            ToggleQueryEditorMaximize();
        }

        private void OnResultsPaneMaximizeToggleRequested(object? sender, EventArgs e)
        {
            ToggleResultsPaneMaximize();
        }

        private void ToggleMaximizeBasedOnFocus()
        {
            // Determine which pane has focus and toggle accordingly
            if (_resultsPane.HasFocus)
            {
                ToggleResultsPaneMaximize();
            }
            else
            {
                // Default to query editor if focus is unclear
                ToggleQueryEditorMaximize();
            }
        }

        private void ToggleQueryEditorMaximize()
        {
            if (_isQueryEditorMaximized)
            {
                RestoreNormalLayout();
            }
            else
            {
                MaximizeQueryEditor();
            }
        }

        private void ToggleResultsPaneMaximize()
        {
            if (_isResultsPaneMaximized)
            {
                RestoreNormalLayout();
            }
            else
            {
                MaximizeResultsPane();
            }
        }

        private void MaximizeQueryEditor()
        {
            // First restore any existing maximized state
            if (_isResultsPaneMaximized)
            {
                _isResultsPaneMaximized = false;
            }
            
            _isQueryEditorMaximized = true;
            
            // Hide connection and results frames
            _leftFrame.Visible = false;
            _rightBottomFrame.Visible = false;
            
            // Expand query editor frame to fill entire window
            _rightTopFrame.X = 0;
            _rightTopFrame.Width = Dim.Fill();
            _rightTopFrame.Height = Dim.Fill();
            _rightTopFrame.Title = "Query Editor (Maximized - F12 to restore)";
            
            // Ensure query editor gets focus
            _queryEditorPane.FocusEditor();
            
            // Trigger layout refresh
            SetNeedsLayout();
        }

        private void MaximizeResultsPane()
        {
            // First restore any existing maximized state
            if (_isQueryEditorMaximized)
            {
                _isQueryEditorMaximized = false;
            }
            
            _isResultsPaneMaximized = true;
            
            // Hide connection and query editor frames
            _leftFrame.Visible = false;
            _rightTopFrame.Visible = false;
            
            // Expand results frame to fill entire window
            _rightBottomFrame.X = 0;
            _rightBottomFrame.Y = 0;
            _rightBottomFrame.Width = Dim.Fill();
            _rightBottomFrame.Height = Dim.Fill();
            _rightBottomFrame.Title = "Results (Maximized - F12 to restore)";
            
            // Ensure results pane gets focus
            _resultsPane.SetFocus();
            
            // Trigger layout refresh
            SetNeedsLayout();
        }

        private void RestoreNormalLayout()
        {
            _isQueryEditorMaximized = false;
            _isResultsPaneMaximized = false;
            
            // Show all frames
            _leftFrame.Visible = true;
            _rightTopFrame.Visible = true;
            _rightBottomFrame.Visible = true;
            
            // Restore original dimensions for query editor frame
            _rightTopFrame.X = 31;
            _rightTopFrame.Width = Dim.Fill();
            _rightTopFrame.Height = _originalRightFrameHeight;
            _rightTopFrame.Title = "Query Editor";
            
            // Restore original dimensions for results frame
            _rightBottomFrame.X = 31;
            _rightBottomFrame.Y = _originalBottomFrameY;
            _rightBottomFrame.Width = Dim.Fill();
            _rightBottomFrame.Height = _originalBottomFrameHeight;
            _rightBottomFrame.Title = "Results";
            
            // Trigger layout refresh
            SetNeedsLayout();
        }

        private async Task ExecuteQueryAsync(string query)
        {
            // If there's an existing query, cancel it but don't wait
            if (_queryCancellationTokenSource != null && !_queryCancellationTokenSource.Token.IsCancellationRequested)
            {
                var oldCancellationSource = _queryCancellationTokenSource;
                var oldClient = _currentKustoClient;
                
                // Cancel the old query
                oldCancellationSource.Cancel();
                
                // Clean up the old resources in the background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (oldClient != null)
                        {
                            await oldClient.CancelCurrentQueryAsync();
                            oldClient.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Failed to clean up old query: {ex.Message}");
                    }
                    finally
                    {
                        oldCancellationSource?.Dispose();
                    }
                });
            }
            
            // Create new cancellation token source for this query
            _queryCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _queryCancellationTokenSource.Token;
            
            try
            {
                var connection = _connectionPane.GetSelectedConnection();
                if (connection == null)
                {
                    Application.Invoke(() => _queryEditorPane.SetExecuting(false));
                    return;
                }

                // Create progress handler
                var progress = new Progress<string>(message =>
                {
                    Application.Invoke(() =>
                    {
                        _queryEditorPane.UpdateProgressMessage(message);
                    });
                });
                var authProvider = AuthenticationProviderFactory.CreateProvider(connection.AuthType);
                _currentKustoClient = new Core.Services.KustoClient(connection, authProvider);
                var result = await _currentKustoClient.ExecuteQueryAsync(query, cancellationToken, progress);
                _currentKustoClient.Dispose();
                _currentKustoClient = null;
                
                Application.Invoke(() =>
                {
                    _queryEditorPane.SetExecuting(false);
                    _resultsPane.DisplayResult(result);
                });
            }
            catch (OperationCanceledException)
            {
                Application.Invoke(() =>
                {
                    _queryEditorPane.SetExecuting(false);
                });
            }
            catch (Exception ex)
            {
                Application.Invoke(() =>
                {
                    _queryEditorPane.SetExecuting(false);
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


        public static IDisposable Run(IConnectionManager connectionManager, IUserSettingsManager userSettingsManager)
        {
            var mainWindow = new MainWindow(connectionManager, userSettingsManager);
            Application.Run(mainWindow);
            return mainWindow;
        }
        
        private async void LoadLastQueryAsync()
        {
            try
            {
                // Load the last query when the window is set up
                _queryEditorPane.LoadLastQueryAsync();
            }
            catch (Exception ex)
            {
                // Silently fail - don't crash the app if we can't load the last query
                Console.WriteLine($"Warning: Failed to load last query: {ex.Message}");
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Save current query before disposing
                try
                {
                    _queryEditorPane?.SaveCurrentQueryAsync();
                }
                catch (Exception ex)
                {
                    // Silently fail - don't crash during shutdown
                    Console.WriteLine($"Warning: Failed to save query during shutdown: {ex.Message}");
                }
            }
            base.Dispose(disposing);
        }
    }
}
