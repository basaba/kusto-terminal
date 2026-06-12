using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;
using KustoTerminal.Core.Services;
using KustoTerminal.UI.Dialogs;
using KustoTerminal.UI.Models;
using Terminal.Gui.Input;
using System.Collections.ObjectModel;
using Terminal.Gui.Drivers;
using Kusto.Cloud.Platform.Utils;
using KustoTerminal.UI.Common;
using Terminal.Gui.Drawing;

namespace KustoTerminal.UI.Panes;

public class ConnectionPane : View
    {
        private readonly IConnectionManager _connectionManager;
        private TreeView _connectionsTree = null!;
        private TextField _searchField = null!;
        private Label[] _shortcutsLabels = null!;
        
        private KustoConnection[] _connections = Array.Empty<KustoConnection>();
        private KustoConnection? _selectedConnection;
        private readonly Dictionary<string, IKustoClient> _kustoClients = new();
        private string _searchQuery = string.Empty;

        public event EventHandler<KustoConnection>? ConnectionSelected;

        public ConnectionPane(IConnectionManager connectionManager)
        {
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));

            InitializeComponents();
            SetupLayout();
            SetKeyboard();
            LoadConnections();
            CanFocus = true;
        }

        private void InitializeComponents()
        {
            _searchField = new TextField()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = 1,
                Visible = false,
            };

            _connectionsTree = new TreeView()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                AllowLetterBasedNavigation = false,
                Style = new TreeStyle()
                {
                    ExpandableSymbol = Glyphs.RightArrow,
                    CollapseableSymbol = Glyphs.DownArrow,
                }
            };
            
            var labels = new List<Label>();
            labels.AddRange(BuildShortcutLabel("n", "new", Pos.Bottom(_connectionsTree) - 5));
            labels.AddRange(BuildShortcutLabel("e", "edit", Pos.Bottom(_connectionsTree) - 4));
            labels.AddRange(BuildShortcutLabel("d", "delete", Pos.Bottom(_connectionsTree) - 3));
            labels.AddRange(BuildShortcutLabel("space", "refresh", Pos.Bottom(_connectionsTree) - 2));
            labels.AddRange(BuildShortcutLabel("/", "search", Pos.Bottom(_connectionsTree) - 1));
            _shortcutsLabels = labels.ToArray();

            // Set up event handlers
            _connectionsTree.SelectionChanged += OnTreeSelectionChanged;
            _connectionsTree.ObjectActivated += OnTreeObjectActivated;
            _searchField.TextChanged += OnSearchTextChanged;
        }

        private static List<Label> BuildShortcutLabel(string shortcutKey, string description, Pos y)
        {
            var keyLabel = new Label()
            {
                Text = $"{shortcutKey}",
                X = 0,
                Y = y,
                Width = Dim.Auto(DimAutoStyle.Text),
                Height = 1,
                SchemeName = Constants.ShortcutKeySchemeName
            };

            var descriptionLabel = keyLabel.AppendLabel($" {description}", Constants.ShortcutDescriptionSchemeName);
            return new List<Label>() { keyLabel, descriptionLabel };
        }

        private void SetKeyboard()
        {
            _connectionsTree.KeyDown += (o, key) =>
            {
                if (key.KeyCode == Key.N.KeyCode)
                {
                    OnAddClicked();
                    key.Handled = true;
                }
                else if (key.KeyCode == Key.E.KeyCode)
                {
                    OnEditClicked();
                    key.Handled = true;
                }
                else if (key == Key.D.KeyCode)
                {
                    OnDeleteClicked();
                    key.Handled = true;
                }
                else if (key == Key.Space)
                {
                    OnExpandDatabases();
                    key.Handled = true;
                }
                else if ((char)key == '/')
                {
                    ActivateSearch();
                    key.Handled = true;
                }
            };

            _searchField.KeyDown += (o, key) =>
            {
                if (key.KeyCode == Key.Esc.KeyCode)
                {
                    CancelSearch();
                    key.Handled = true;
                }
                else if (key.KeyCode == Key.Enter.KeyCode)
                {
                    CommitSearch();
                    key.Handled = true;
                }
            };
        }

        private void ActivateSearch()
        {
            _searchField.Visible = true;
            _connectionsTree.Y = 1;
            _searchField.Text = string.Empty;
            _searchField.SetFocus();
            SetNeedsLayout();
        }

        private void CancelSearch()
        {
            _searchQuery = string.Empty;
            _searchField.Text = string.Empty;
            HideSearch();
            RebuildTree();
        }

        private void CommitSearch()
        {
            HideSearch();
            _connectionsTree.SetFocus();
        }

        private void HideSearch()
        {
            _searchField.Visible = false;
            _connectionsTree.Y = 0;
            SetNeedsLayout();
        }

        private void OnSearchTextChanged(object? sender, EventArgs e)
        {
            _searchQuery = _searchField.Text?.ToString() ?? string.Empty;
            RebuildTree();
        }

        private void SetupLayout()
        {
            Add(_searchField);
            Add(_connectionsTree);
            _shortcutsLabels.ToList().ForEach(label => Add(label));
        }

        private async void LoadConnections()
        {
            try
            {
                var connections = await _connectionManager.GetConnectionsAsync();
                _connections = connections.ToArray();

                RebuildTree();

                if (_connections.Length > 0)
                {
                    _selectedConnection = _connections[0];
                }
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery("Error", $"Failed to load connections: {ex.Message}", "OK");
            }
        }

        private void RebuildTree()
        {
            _connectionsTree.ClearObjects();

            foreach (var connection in _connections)
            {
                if (!MatchesSearch(connection))
                    continue;

                var clusterNode = new ClusterTreeNode(connection, GetKustoClient(connection), _connectionManager);
                _connectionsTree.AddObject(clusterNode);
            }
        }

        private bool MatchesSearch(KustoConnection connection)
        {
            if (string.IsNullOrEmpty(_searchQuery))
                return true;

            var q = _searchQuery;
            var cmp = StringComparison.OrdinalIgnoreCase;

            if (!string.IsNullOrEmpty(connection.Name) && connection.Name.Contains(q, cmp))
                return true;
            if (!string.IsNullOrEmpty(connection.ClusterUri) && connection.ClusterUri.Contains(q, cmp))
                return true;
            if (connection.GetClusterNameFromUrl().Contains(q, cmp))
                return true;
            if (!string.IsNullOrEmpty(connection.Database) && connection.Database.Contains(q, cmp))
                return true;
            if (connection.Databases != null && connection.Databases.Any(db => db.Contains(q, cmp)))
                return true;

            return false;
        }

        public void RefreshConnections()
        {
            LoadConnections();
        }

        private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs<ITreeNode> args)
        {
            var selectedNode = _connectionsTree.SelectedObject;
            
            if (selectedNode is ClusterTreeNode clusterNode)
            {
                _selectedConnection = clusterNode.Connection;
            }
            else if (selectedNode is DatabaseTreeNode dbNode)
            {
                _selectedConnection = dbNode.ParentConnection;
            }
            else
            {
                _selectedConnection = null;
            }
        }

        private void OnExpandDatabases()
        {
            var selectedNode = _connectionsTree.SelectedObject;
            
            if (selectedNode is ClusterTreeNode clusterNode)
            {
                // Load and expand databases for the selected cluster
                Task.Run(async () => {
                    // Refresh immediately to show loading state
                    Application.Invoke(() => _connectionsTree.RefreshObject(clusterNode));
                    
                    await clusterNode.LoadDatabasesAsync(forceRefresh: true);
                    
                    // Refresh again after loading is complete
                    Application.Invoke(() =>
                    {
                        _connectionsTree.RefreshObject(clusterNode);
                        _connectionsTree.Expand(clusterNode);
                    });
                });
            }
        }

        private void OnTreeObjectActivated(object? sender, ObjectActivatedEventArgs<ITreeNode> args)
        {
            if (args.ActivatedObject is ClusterTreeNode clusterNode)
            {
                OnConnectClicked();
            }
            else if (args.ActivatedObject is DatabaseTreeNode dbNode)
            {
                // When a database is activated, update the connection's database and connect
                _selectedConnection = new KustoConnection
                {
                    Id = dbNode.ParentConnection.Id,
                    Name = dbNode.ParentConnection.Name,
                    ClusterUri = dbNode.ParentConnection.ClusterUri,
                    Database = dbNode.DatabaseName,
                    Databases = dbNode.ParentConnection.Databases,
                    AuthType = dbNode.ParentConnection.AuthType,
                };
                OnConnectClicked();
            }
        }

        private void OnAddClicked()
        {
            var dialog = new ConnectionDialog();
            Application.Run(dialog);

            if (dialog.Result != null)
            {
                Task.Run(async () =>
                {
                    await _connectionManager.AddConnectionAsync(dialog.Result);
                    Application.Invoke(() => RefreshConnections());
                });
            }
        }

        private void OnEditClicked()
        {
            if (_selectedConnection == null) return;

            var dialog = new ConnectionDialog(_selectedConnection);
            Application.Run(dialog);

            if (dialog.Result != null)
            {
                Task.Run(async () =>
                {
                    await _connectionManager.UpdateConnectionAsync(dialog.Result);
                    Application.Invoke(() => RefreshConnections());
                });
            }
        }

        private void OnDeleteClicked()
        {
            if (_selectedConnection == null) return;

            var result = MessageBox.Query("Confirm Delete",
                $"Are you sure you want to delete connection '{_selectedConnection.DisplayName}'?",
                "Yes", "No");

            if (result == 0) // Yes
            {
                Task.Run(async () =>
                {
                    await _connectionManager.DeleteConnectionAsync(_selectedConnection.Id);
                    Application.Invoke(() => RefreshConnections());
                });
            }
        }

        private void OnConnectClicked()
        {
            if (_selectedConnection != null)
            {
                ConnectionSelected?.Invoke(this, _selectedConnection);
            }
        }

        public KustoConnection? GetSelectedConnection()
        {
            return _selectedConnection;
        }

        /// <summary>
        /// Finds a saved connection matching the given cluster URI and selects it in the tree.
        /// If no matching connection exists, creates and persists a new one.
        /// If a database is specified and exists as a child node, selects that database node instead.
        /// </summary>
        public async Task SelectByClusterUriAsync(string clusterUri, string? databaseName = null, string? displayName = null)
        {
            if (string.IsNullOrWhiteSpace(clusterUri))
                return;

            // Try to find an existing connection
            var found = TrySelectNode(clusterUri, databaseName);
            if (found)
                return;

            // No matching connection — create and persist a new one
            var newConnection = new KustoConnection
            {
                Name = displayName ?? "",
                ClusterUri = clusterUri,
                Database = databaseName ?? "",
                AuthType = AuthenticationType.AzureCli
            };

            await _connectionManager.AddConnectionAsync(newConnection);
            RefreshConnections();

            // Now select the newly added node
            TrySelectNode(clusterUri, databaseName);
        }

        /// <summary>
        /// Highlights the tree node matching the given connection (cluster + optional database).
        /// Does not create a new connection if no match exists; in that case the current
        /// selection is left unchanged.
        /// </summary>
        public void HighlightConnection(KustoConnection? connection)
        {
            if (connection == null || string.IsNullOrWhiteSpace(connection.ClusterUri))
                return;

            TrySelectNode(connection.ClusterUri, connection.Database);
        }

        private bool TrySelectNode(string clusterUri, string? databaseName)
        {
            foreach (var obj in _connectionsTree.Objects)
            {
                if (obj is not ClusterTreeNode clusterNode)
                    continue;

                if (!clusterNode.Connection.ClusterUri.Equals(clusterUri, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Found matching cluster — expand and select
                _connectionsTree.Expand(clusterNode);

                if (!string.IsNullOrWhiteSpace(databaseName))
                {
                    var dbNode = clusterNode.Children
                        .OfType<DatabaseTreeNode>()
                        .FirstOrDefault(db => db.DatabaseName.Equals(databaseName, StringComparison.OrdinalIgnoreCase));

                    if (dbNode != null)
                    {
                        _connectionsTree.GoTo(dbNode);
                        return true;
                    }
                }

                _connectionsTree.GoTo(clusterNode);
                return true;
            }

            return false;
        }

        private IKustoClient GetKustoClient(KustoConnection connection)
        {
            if (!_kustoClients.TryGetValue(connection.Id, out var client))
            {
                var authProvider = AuthenticationProviderFactory.CreateProvider(connection.AuthType);
                client = new KustoClient(connection, authProvider!);
                _kustoClients[connection.Id] = client;
            }

            return client;
        }
}
