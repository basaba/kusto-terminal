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
using Terminal.Gui.Drivers;

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
                if (key.KeyCode == (KeyCode.CtrlMask | Key.Q.KeyCode)
                || key.KeyCode == (KeyCode.CtrlMask | Key.C.KeyCode))
                {
                    Application.Shutdown();
                    key.Handled = true;
                }
                else if (key == Key.Esc)
                {
                    key.Handled = true;
                }
            };
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

            // Set up events
            _connectionPane.ConnectionSelected += OnConnectionSelected;
            _queryEditorPane.QueryExecuteRequested += OnQueryExecuteRequested;
            _queryEditorPane.EscapePressed += OnQueryEditorEscapePressed;
            _queryEditorPane.QueryCancelRequested += OnQueryCancelRequested;
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