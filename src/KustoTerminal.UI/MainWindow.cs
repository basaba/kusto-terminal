using System;
using System.Collections.Generic;
using System.Linq;
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
using KustoTerminal.Language.Services;
using KustoTerminal.UI.AutoCompletion;
using KustoTerminal.UI.Common;
using KustoTerminal.UI.Controls;
using KustoTerminal.UI.Models;
using KustoTerminal.UI.Services;
using Terminal.Gui.Input;
using Terminal.Gui.Drivers;
using KustoTerminal.UI.SyntaxHighlighting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Terminal.Gui.Drawing;

namespace KustoTerminal.UI;

public class MainWindow : Window
{
    private readonly IConnectionManager _connectionManager;
        private readonly IUserSettingsManager _userSettingsManager;
        private readonly ClusterSchemaService _clusterSchemaService;
        private readonly SyntaxHighlighter _syntaxHighlighter;
        private readonly HtmlSyntaxHighlighter _htmlSyntaxHighlighter;
        
        private ConnectionPane _connectionPane = null!;
        private FrameView _leftFrame = null!;
        private FrameView _rightTopFrame = null!;
        private FrameView _rightBottomFrame = null!;
        private TabBar _tabBar = null!;
        private TabManagerService _tabManager = null!;
        private Label _kustoTerminalLabel = null!;
        private List<Label> _shortcutLabels = null!;
        
        // Maximize state
        private bool _isQueryEditorMaximized = false;
        private bool _isResultsPaneMaximized = false;
        private Dim _originalRightFrameHeight = null!;
        private Pos _originalBottomFrameY = null!;
        private Dim _originalBottomFrameHeight = null!;
        private Pos _originalRightTopFrameY = null!;

        public MainWindow(IConnectionManager connectionManager, IUserSettingsManager userSettingsManager)
        {
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            _userSettingsManager = userSettingsManager ?? throw new ArgumentNullException(nameof(userSettingsManager));

            // Initialize language service and cluster schema service
            var languageService = new LanguageService();
            _syntaxHighlighter = new SyntaxHighlighter(languageService);
            _htmlSyntaxHighlighter = new HtmlSyntaxHighlighter(languageService);
            
            // Initialize cache configuration with default settings
            var cacheConfig = new CacheConfiguration
            {
                EnableDiskCache = true,
                CacheExpirationHours = 24
            };
            _clusterSchemaService = new ClusterSchemaService(languageService, cacheConfig);

            // Initialize tab manager — passes languageService so each tab creates its own autocomplete generator
            _tabManager = new TabManagerService(
                _userSettingsManager,
                _syntaxHighlighter,
                languageService,
                _htmlSyntaxHighlighter);

            X = 0;
            Y = 0;
            Width = Dim.Fill();
            Height = Dim.Fill();
            SchemeName = "MainWindow";

            InitializeComponents();
            SetupLayout();
            SetKeyboard();
            SetupClusterSchemaEvents();
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
                Height = Dim.Fill()! - 1,
                TabStop = TabBehavior.TabStop,
                SchemeName = "FrameView",
                BorderStyle = Terminal.Gui.Drawing.LineStyle.Single,
                Arrangement = ViewArrangement.RightResizable
            };

            _tabBar = new TabBar()
            {
                X = Pos.Right(_leftFrame),
                Y = 0,
                Width = Dim.Fill(),
                Height = 1,
                SchemeName = "FrameView",
            };

            _rightTopFrame = new FrameView()
            {
                Title = "",
                X = Pos.Right(_leftFrame),
                Y = Pos.Bottom(_tabBar),
                Width = Dim.Fill(),
                Height = Dim.Percent(60),
                TabStop = TabBehavior.TabStop,
                SchemeName = "FrameView",
                BorderStyle = Terminal.Gui.Drawing.LineStyle.Single,
                Arrangement = ViewArrangement.BottomResizable
            };

            _rightBottomFrame = new FrameView()
            {
                Title = "Results",
                X = Pos.Right(_leftFrame),
                Y = Pos.Bottom(_rightTopFrame),
                Width = Dim.Fill(),
                Height = Dim.Fill()! - 1,
                TabStop = TabBehavior.TabStop,
                SchemeName = "FrameView",
                BorderStyle = Terminal.Gui.Drawing.LineStyle.Single,
            };
            
            // Create connection pane
            _connectionPane = new ConnectionPane(_connectionManager)
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                SchemeName = "Base",
            };
            
            _kustoTerminalLabel = new Label()
            {
                Title = " Kusto Terminal",
                X = 0,
                Y = Pos.Bottom(_leftFrame),
                Width = Dim.Auto(DimAutoStyle.Text),
                Height = 1,
            };

            _shortcutLabels = BuildShortcutsLabels(_kustoTerminalLabel);
        }

        private static List<Label> BuildShortcutsLabels(Label labelToAppendTo)
        {
            var labels = new List<Label>();
            var last = labelToAppendTo;
            var normalScheme = Constants.ShortcutDescriptionSchemeName;
            var shortcutKeyScheme = Constants.ShortcutKeySchemeName;
            last = last.AppendLabel(" | ", normalScheme, labels);
            last = last.AppendLabel("ctrl+q", shortcutKeyScheme, labels);
            last = last.AppendLabel(" quit ", normalScheme, labels);
            last = last.AppendLabel("| ", normalScheme, labels);
            last = last.AppendLabel("alt+t/f7", shortcutKeyScheme, labels);
            last = last.AppendLabel(" new tab ", normalScheme, labels);
            last = last.AppendLabel("| ", normalScheme, labels);
            last = last.AppendLabel("alt+w/shift+f7", shortcutKeyScheme, labels);
            last = last.AppendLabel(" close tab ", normalScheme, labels);
            last = last.AppendLabel("| ", normalScheme, labels);
            last = last.AppendLabel("f8/shift+f8", shortcutKeyScheme, labels);
            last = last.AppendLabel(" switch tabs ", normalScheme, labels);
            return labels;
        }

        private void SetupLayout()
        {
            // Add frames to window
            Add(_leftFrame, _tabBar, _rightTopFrame, _rightBottomFrame, _kustoTerminalLabel);
            Add(_shortcutLabels.ToArray());

            // Add connection pane to left frame
            _leftFrame.Add(_connectionPane);
            
            // Set initial focus to connection pane
            _connectionPane.SetFocus();

            // Set up connection pane events
            _connectionPane.ConnectionSelected += OnConnectionSelected;

            // Set up tab bar events
            _tabBar.TabSelected += OnTabBarTabSelected;
            _tabBar.TabCloseRequested += OnTabBarTabCloseRequested;
            _tabBar.NewTabRequested += OnTabBarNewTabRequested;

            // Track focus to dim tabs when editor is not focused
            _rightTopFrame.HasFocusChanged += (_, e) => _tabBar.IsEditorFocused = e.NewValue;

            // Set up tab manager events
            _tabManager.ActiveTabChanged += OnActiveTabChanged;
            _tabManager.TabCreated += OnTabCreated;
            
            // Store original dimensions for restore
            _originalRightTopFrameY = _rightTopFrame.Y!;
            _originalRightFrameHeight = _rightTopFrame.Height!;
            _originalBottomFrameY = _rightBottomFrame.Y!;
            _originalBottomFrameHeight = _rightBottomFrame.Height!;
            
            // Create a default tab immediately so frames are never empty
            _tabManager.CreateTab();
            
            // Then try to restore saved tabs asynchronously (replaces default if saved tabs exist)
            RestoreSavedTabsAsync();
        }

        private void RestoreSavedTabsAsync()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var tabStates = await _userSettingsManager.GetTabsAsync();
                    if (tabStates.Count > 0)
                    {
                        var connections = await _connectionManager.GetConnectionsAsync();
                        Application.Invoke(() =>
                        {
                            // Close the default tab and restore saved tabs
                            // First, dispose the existing default tab cleanly
                            var defaultTab = _tabManager.ActiveTab;
                            
                            // Clear and restore
                            _tabManager.RestoreTabs(tabStates, _connectionManager);
                            
                            // Restore connections for tabs
                            foreach (var tab in _tabManager.Tabs)
                            {
                                if (!string.IsNullOrEmpty(tab.State.ConnectionId))
                                {
                                    var conn = connections.FirstOrDefault(c => c.Id == tab.State.ConnectionId);
                                    if (conn != null)
                                    {
                                        var tabConn = new KustoConnection
                                        {
                                            Id = conn.Id,
                                            Name = conn.Name,
                                            ClusterUri = conn.ClusterUri,
                                            Database = tab.State.Database ?? conn.Database,
                                            Databases = conn.Databases,
                                            AuthType = conn.AuthType
                                        };
                                        tab.Connection = tabConn;
                                        tab.RestoreConnectionLabel();
                                    }
                                }
                            }
                            
                            UpdateEditorFrameTitle();
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to restore saved tabs: {ex.Message}");
                }
            });
        }

        private static bool IsAltKey(Key key, char letter)
        {
            // Check for explicit AltMask (Linux/kitty protocol)
            var lower = (KeyCode)(letter | 0x20);
            var upper = (KeyCode)(letter & ~0x20);
            var stripped = key.KeyCode & ~KeyCode.AltMask;
            if (key.KeyCode.HasFlag(KeyCode.AltMask) && (stripped == lower || stripped == upper))
                return true;

            // macOS: terminal resolves Alt+letter into a Unicode character (no AltMask).
            // Match the Unicode codepoint that macOS produces.
            int macChar = letter switch
            {
                't' or 'T' => 0x2020, // † (dagger)
                'w' or 'W' => 0x2211, // ∑ (summation)
                _ => 0
            };
            return macChar != 0 && key.KeyCode == (KeyCode)macChar;
        }

        private void SetKeyboard()
        {
            // Application-level handler intercepts Alt+key BEFORE any view processes them.
            // This is necessary because TextView consumes Alt+letter as special chars.
            Application.KeyDown += (o, key) =>
            {
                if (IsAltKey(key, 't'))
                {
                    CreateNewTab();
                    key.Handled = true;
                }
                else if (IsAltKey(key, 'w'))
                {
                    CloseActiveTab();
                    key.Handled = true;
                }
                else if (key.KeyCode == (KeyCode.AltMask | KeyCode.Tab)
                      || key.KeyCode == (KeyCode.Tab | KeyCode.AltMask))
                {
                    _tabManager.ActivateNextTab();
                    UpdateEditorFrameTitle();
                    key.Handled = true;
                }
                else if (key.KeyCode == (KeyCode.AltMask | KeyCode.ShiftMask | KeyCode.Tab)
                      || key.KeyCode == (KeyCode.ShiftMask | KeyCode.AltMask | KeyCode.Tab))
                {
                    _tabManager.ActivatePreviousTab();
                    UpdateEditorFrameTitle();
                    key.Handled = true;
                }
            };

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
                // Tab management shortcuts
                else if (key == Key.F7)
                {
                    CreateNewTab();
                    key.Handled = true;
                }
                else if (key == (KeyCode.ShiftMask | Key.F7.KeyCode))
                {
                    CloseActiveTab();
                    key.Handled = true;
                }
                else if (key == Key.F8)
                {
                    _tabManager.ActivateNextTab();
                    key.Handled = true;
                }
                else if (key == (KeyCode.ShiftMask | Key.F8.KeyCode))
                {
                    _tabManager.ActivatePreviousTab();
                    key.Handled = true;
                }
                // Alt+1..9 for direct tab switching (safe — not letter keys)
                else if (TryGetAltNumberKey(key, out var tabNumber))
                {
                    _tabManager.ActivateTabByNumber(tabNumber);
                    key.Handled = true;
                }
                // Frame navigation
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
                    if (_rightTopFrame.HasFocus || _leftFrame.HasFocus)
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
            
            // We want to prevent the user navigating inside each frame from mistakenly change the focus unintentionally
            // to different frame.
            _rightBottomFrame.KeyDown += HandleCursors;
            _rightTopFrame.KeyDown += HandleCursors;
            _leftFrame.KeyDown += HandleCursors;
        }

        private static bool TryGetAltNumberKey(Key key, out int number)
        {
            number = 0;
            // Check for Alt+1 through Alt+9
            for (int i = 1; i <= 9; i++)
            {
                if (key.KeyCode == (KeyCode.AltMask | (KeyCode)('0' + i)))
                {
                    number = i;
                    return true;
                }
            }
            return false;
        }
        
        private void HandleCursors(object? sender, Key key)
        {
            if (key == Key.CursorRight
                || key == Key.CursorLeft
                || key == Key.CursorUp
                || key == Key.CursorDown)
            {
                key.Handled = true;
            }
        }

        #region Tab Management

        private void CreateNewTab()
        {
            if (_tabManager.Tabs.Count >= TabManagerService.MaxTabs) return;

            _tabManager.CreateTab();
            UpdateEditorFrameTitle();
        }

        private void OnTabCreated(object? sender, QueryTab tab)
        {
            WireTabEvents(tab);
        }

        private void CloseActiveTab()
        {
            if (_tabManager.Tabs.Count <= 1) return;

            var activeIndex = _tabManager.ActiveTabIndex;
            var tab = _tabManager.ActiveTab;
            if (tab != null)
            {
                UnwireTabEvents(tab);
            }
            _tabManager.CloseTab(activeIndex);
            UpdateEditorFrameTitle();
        }

        private void WireTabEvents(QueryTab tab)
        {
            tab.EditorPane.QueryExecuteRequested += OnQueryExecuteRequested;
            tab.EditorPane.QueryCancelRequested += OnQueryCancelRequested;
            tab.EditorPane.MaximizeToggleRequested += OnQueryEditorMaximizeToggleRequested;
            tab.ResultsPane.MaximizeToggleRequested += OnResultsPaneMaximizeToggleRequested;
        }

        private void UnwireTabEvents(QueryTab tab)
        {
            tab.EditorPane.QueryExecuteRequested -= OnQueryExecuteRequested;
            tab.EditorPane.QueryCancelRequested -= OnQueryCancelRequested;
            tab.EditorPane.MaximizeToggleRequested -= OnQueryEditorMaximizeToggleRequested;
            tab.ResultsPane.MaximizeToggleRequested -= OnResultsPaneMaximizeToggleRequested;
        }

        private void OnActiveTabChanged(object? sender, QueryTab newTab)
        {
            // Remove old panes from frames
            _rightTopFrame.RemoveAll();
            _rightBottomFrame.RemoveAll();

            // Add new tab's panes to frames
            _rightTopFrame.Add(newTab.EditorPane);
            _rightBottomFrame.Add(newTab.ResultsPane);

            // Update frame title to show active tab
            UpdateEditorFrameTitle();

            // Focus the editor
            newTab.EditorPane.FocusEditor();

            SetNeedsLayout();
        }

        private void OnTabBarTabSelected(object? sender, int index)
        {
            _tabManager.ActivateTab(index);
        }

        private void OnTabBarTabCloseRequested(object? sender, int index)
        {
            if (_tabManager.Tabs.Count <= 1) return;

            var tab = _tabManager.Tabs[index];
            UnwireTabEvents(tab);
            _tabManager.CloseTab(index);
            UpdateEditorFrameTitle();
        }

        private void OnTabBarNewTabRequested(object? sender, EventArgs e)
        {
            CreateNewTab();
        }

        private void UpdateEditorFrameTitle()
        {
            // Refresh the tab bar display
            var titles = _tabManager.GetTabTitles();
            _tabBar.SetTabs(titles, _tabManager.ActiveTabIndex);
        }

        #endregion

        #region Connection Events

        private void OnConnectionSelected(object? sender, KustoConnection connection)
        {
            var activeTab = _tabManager.ActiveTab;
            if (activeTab != null)
            {
                activeTab.Connection = connection;
                activeTab.EditorPane.SetConnection(connection);
                activeTab.EditorPane.FocusEditor();
                UpdateEditorFrameTitle();
            }

            // Ensure schema is loaded for the selected connection so autocompletion works
            // immediately. Uses cache when available, so this is fast if the startup
            // background load already completed.
            _ = Task.Run(async () =>
            {
                await _clusterSchemaService.FetchAndUpdateClusterSchemaAsync(connection);
            });
        }

        #endregion

        #region Query Execution

        private async void OnQueryExecuteRequested(object? sender, string query)
        {
            await ExecuteQueryAsync(query);
        }

        private void OnQueryCancelRequested(object? sender, EventArgs e)
        {
            var activeTab = _tabManager.ActiveTab;
            if (activeTab == null) return;

            if (activeTab.CancellationTokenSource != null && !activeTab.CancellationTokenSource.Token.IsCancellationRequested)
            {
                activeTab.CancellationTokenSource.Cancel();
                
                Application.Invoke(() => activeTab.EditorPane.SetExecuting(false));
                
                if (activeTab.CurrentKustoClient != null)
                {
                    var clientToCancel = activeTab.CurrentKustoClient;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await clientToCancel.CancelCurrentQueryAsync();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Warning: Server-side cancellation failed: {ex.Message}");
                        }
                    });
                }
            }
        }

        private async Task ExecuteQueryAsync(string query)
        {
            var tab = _tabManager.ActiveTab;
            if (tab == null) return;

            // If there's an existing query on this tab, cancel it
            if (tab.CancellationTokenSource != null && !tab.CancellationTokenSource.Token.IsCancellationRequested)
            {
                var oldCancellationSource = tab.CancellationTokenSource;
                var oldClient = tab.CurrentKustoClient;
                
                oldCancellationSource.Cancel();
                
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
            
            tab.CancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = tab.CancellationTokenSource.Token;
            
            try
            {
                var connection = tab.Connection ?? _connectionPane.GetSelectedConnection();
                if (connection == null)
                {
                    Application.Invoke(() => tab.EditorPane.SetExecuting(false));
                    return;
                }

                // Ensure the tab's connection is set
                if (tab.Connection == null)
                {
                    tab.Connection = connection;
                    tab.EditorPane.SetConnection(connection);
                    UpdateEditorFrameTitle();
                }

                // Handle #connect directive — switch connection context before executing
                if (ConnectDirectiveParser.TryParse(query, out var connectClusterUri, out var connectDisplayName, out var connectDatabase, out var connectRemainingQuery))
                {
                    var newConnection = new KustoConnection
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = connectDisplayName,
                        ClusterUri = connectClusterUri,
                        Database = connectDatabase ?? "",
                        AuthType = connection.AuthType
                    };

                    tab.Connection = newConnection;
                    connection = newConnection;
                    Application.Invoke(async () =>
                    {
                        tab.EditorPane.SetConnection(newConnection);
                        await _connectionPane.SelectByClusterUriAsync(connectClusterUri, connectDatabase, connectDisplayName);
                        UpdateEditorFrameTitle();
                    });

                    // Load schema and resolve databases for the new cluster
                    _ = Task.Run(async () =>
                    {
                        await _clusterSchemaService.FetchAndUpdateClusterSchemaAsync(newConnection);
                    });

                    // If no database specified, just switch cluster context without running
                    if (string.IsNullOrWhiteSpace(connectDatabase))
                    {
                        Application.Invoke(() => tab.EditorPane.SetExecuting(false));
                        return;
                    }

                    // If no remaining query after the directive, just switch context
                    if (string.IsNullOrWhiteSpace(connectRemainingQuery))
                    {
                        Application.Invoke(() => tab.EditorPane.SetExecuting(false));
                        return;
                    }

                    query = connectRemainingQuery;
                }

                var progress = new Progress<string>(message =>
                {
                    Application.Invoke(() =>
                    {
                        tab.EditorPane.UpdateProgressMessage(message);
                    });
                });
                var authProvider = AuthenticationProviderFactory.CreateProvider(connection.AuthType)!;
                tab.CurrentKustoClient = new Core.Services.KustoClient(connection, authProvider);
                var result = await tab.CurrentKustoClient.ExecuteQueryAsync(query, cancellationToken, progress);
                tab.CurrentKustoClient.Dispose();
                tab.CurrentKustoClient = null;
                
                Application.Invoke(() =>
                {
                    tab.EditorPane.SetExecuting(false);
                    tab.ResultsPane.SetQueryText(query);
                    tab.ResultsPane.SetConnection(connection);
                    tab.ResultsPane.DisplayResult(result);
                });
            }
            catch (OperationCanceledException)
            {
                Application.Invoke(() =>
                {
                    tab.EditorPane.SetExecuting(false);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: Query execution failed: {ex.Message}");
                Application.Invoke(() =>
                {
                    tab.EditorPane.SetExecuting(false);
                });
            }
            finally
            {
                if (tab.CancellationTokenSource != null)
                {
                    tab.CancellationTokenSource.Dispose();
                    tab.CancellationTokenSource = null;
                }
                
                if (tab.CurrentKustoClient != null)
                {
                    tab.CurrentKustoClient.Dispose();
                    tab.CurrentKustoClient = null;
                }
            }
        }

        #endregion

        #region Maximize

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
            var activeTab = _tabManager.ActiveTab;
            if (activeTab != null && activeTab.ResultsPane.HasFocus)
            {
                ToggleResultsPaneMaximize();
            }
            else
            {
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
            if (_isResultsPaneMaximized)
            {
                _isResultsPaneMaximized = false;
            }
            
            _isQueryEditorMaximized = true;
            
            _leftFrame.Visible = false;
            _rightBottomFrame.Visible = false;
            _tabBar.Visible = false;
            
            _rightTopFrame.X = 0;
            _rightTopFrame.Y = 0;
            _rightTopFrame.Width = Dim.Fill();
            _rightTopFrame.Height = Dim.Fill();
            _rightTopFrame.Title = "Maximized - F12 to restore";
            
            _tabManager.ActiveTab?.EditorPane.FocusEditor();
            
            SetNeedsLayout();
        }

        private void MaximizeResultsPane()
        {
            if (_isQueryEditorMaximized)
            {
                _isQueryEditorMaximized = false;
            }
            
            _isResultsPaneMaximized = true;
            
            _leftFrame.Visible = false;
            _rightTopFrame.Visible = false;
            _tabBar.Visible = false;
            
            _rightBottomFrame.X = 0;
            _rightBottomFrame.Y = 0;
            _rightBottomFrame.Width = Dim.Fill();
            _rightBottomFrame.Height = Dim.Fill();
            _rightBottomFrame.Title = "Results (Maximized - F12 to restore)";
            
            _tabManager.ActiveTab?.ResultsPane.SetFocus();
            
            SetNeedsLayout();
        }

        private void RestoreNormalLayout()
        {
            _isQueryEditorMaximized = false;
            _isResultsPaneMaximized = false;
            
            _leftFrame.Visible = true;
            _rightTopFrame.Visible = true;
            _rightBottomFrame.Visible = true;
            _tabBar.Visible = true;
            
            _rightTopFrame.X = Pos.Right(_leftFrame);
            _rightTopFrame.Y = _originalRightTopFrameY;
            _rightTopFrame.Width = Dim.Fill();
            _rightTopFrame.Height = _originalRightFrameHeight;
            _rightTopFrame.Title = "";
            
            _rightBottomFrame.X = Pos.Right(_leftFrame);
            _rightBottomFrame.Y = _originalBottomFrameY;
            _rightBottomFrame.Width = Dim.Fill();
            _rightBottomFrame.Height = _originalBottomFrameHeight;
            _rightBottomFrame.Title = "Results";
            
            SetNeedsLayout();
        }

        #endregion

        public static MainWindow Create(IConnectionManager connectionManager, IUserSettingsManager userSettingsManager)
        {
            return new MainWindow(connectionManager, userSettingsManager);
        }
        
        private void SetupClusterSchemaEvents()
        {
            _connectionManager.ConnectionAddOrUpdated += OnConnectionAddedOrUpdated;
            
            _ = Task.Run(async () =>
            {
                try
                {
                    var connections = await _connectionManager.GetConnectionsAsync();
                    await _clusterSchemaService.FetchAndUpdateMultipleClusterSchemasAsync(connections);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to load cluster schemas on startup: {ex.Message}");
                }
            });
        }

        private void OnConnectionAddedOrUpdated(object? sender, KustoConnection connection)
        {
            _ = Task.Run(async () =>
            {
                await _clusterSchemaService.FetchAndUpdateClusterSchemaAsync(connection, forceRefresh: true);
            });
        }

        private async Task SaveTabsAsync()
        {
            try
            {
                var tabStates = _tabManager.GetTabStates();
                await _userSettingsManager.SaveTabsAsync(tabStates);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to save tabs: {ex.Message}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _connectionManager.ConnectionAddOrUpdated -= OnConnectionAddedOrUpdated;
                
                // Save tabs before disposing
                try
                {
                    SaveTabsAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to save tabs before exit: {ex.Message}");
                }
                
                // Unwire events from all tabs
                foreach (var tab in _tabManager.Tabs)
                {
                    UnwireTabEvents(tab);
                }
                
                _tabManager.Dispose();
            }
            base.Dispose(disposing);
        }
    }
