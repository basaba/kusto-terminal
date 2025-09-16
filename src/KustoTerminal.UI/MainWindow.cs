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
        private FrameView _rightFrame;
        private FrameView _bottomFrame;
        
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

            Title = "Kusto Terminal";
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
        }

        private void SetupLayout()
        {
            // Add frames to window
            Add(_leftFrame, _rightFrame, _bottomFrame);

            // Add panes to frames
            _leftFrame.Add(_connectionPane);
            _rightFrame.Add(_queryEditorPane);
            _bottomFrame.Add(_resultsPane);
            
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
            _originalRightFrameHeight = _rightFrame.Height;
            _originalBottomFrameY = _bottomFrame.Y;
            _originalBottomFrameHeight = _bottomFrame.Height;
            
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

        private async void OnQueryCancelRequested(object? sender, EventArgs e)
        {
            // Cancel the current query if one is running
            if (_queryCancellationTokenSource != null && !_queryCancellationTokenSource.Token.IsCancellationRequested)
            {                
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
                        
                    }
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
            _bottomFrame.Visible = false;
            
            // Expand query editor frame to fill entire window
            _rightFrame.X = 0;
            _rightFrame.Width = Dim.Fill();
            _rightFrame.Height = Dim.Fill();
            _rightFrame.Title = "Query Editor (Maximized - F12 to restore)";
            
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
            _rightFrame.Visible = false;
            
            // Expand results frame to fill entire window
            _bottomFrame.X = 0;
            _bottomFrame.Y = 0;
            _bottomFrame.Width = Dim.Fill();
            _bottomFrame.Height = Dim.Fill();
            _bottomFrame.Title = "Results (Maximized - F12 to restore)";
            
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
            _rightFrame.Visible = true;
            _bottomFrame.Visible = true;
            
            // Restore original dimensions for query editor frame
            _rightFrame.X = 31;
            _rightFrame.Width = Dim.Fill();
            _rightFrame.Height = _originalRightFrameHeight;
            _rightFrame.Title = "Query Editor";
            
            // Restore original dimensions for results frame
            _bottomFrame.X = 31;
            _bottomFrame.Y = _originalBottomFrameY;
            _bottomFrame.Width = Dim.Fill();
            _bottomFrame.Height = _originalBottomFrameHeight;
            _bottomFrame.Title = "Results";
            
            // Trigger layout refresh
            SetNeedsLayout();
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
