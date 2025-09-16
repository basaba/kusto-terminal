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
using Terminal.Gui.Drawing;

namespace KustoTerminal.UI.Panes
{
    public class ConnectionPane : View
    {
        private readonly IConnectionManager _connectionManager;
        private TreeView _connectionsTree;
        private Label[] _shortcutsLabels;

        
        private KustoConnection[] _connections = Array.Empty<KustoConnection>();
        private KustoConnection? _selectedConnection;
        private readonly Dictionary<string, IKustoClient> _kustoClients = new();

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
            _connectionsTree = new TreeView()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Style = new TreeStyle()
                {
                    ExpandableSymbol = Glyphs.RightArrow,
                    CollapseableSymbol = Glyphs.DownArrow,
                }
            };

            _shortcutsLabels = new[]{
                new Label()
                {
                    Text = "Ctrl+N: New",
                    X = 0,
                    Y = Pos.Bottom(_connectionsTree) - 4,
                    Width = Dim.Fill(),
                    Height = 1
                },
                new Label()
                {
                    Text = "Ctrl+E: Edit",
                    X = 0,
                    Y = Pos.Bottom(_connectionsTree) - 3,
                    Width = Dim.Fill(),
                    Height = 1
                },
                new Label()
                {
                    Text = "Del: Delete",
                    X = 0,
                    Y = Pos.Bottom(_connectionsTree) - 2,
                    Width = Dim.Fill(),
                    Height = 1
                },
                new Label()
                {
                    Text = "Space: Expand DBs",
                    X = 0,
                    Y = Pos.Bottom(_connectionsTree) - 1,
                    Width = Dim.Fill(),
                    Height = 1
                }
            };

            // Set up event handlers
            _connectionsTree.SelectionChanged += OnTreeSelectionChanged;
            _connectionsTree.ObjectActivated += OnTreeObjectActivated;
        }

        private void SetKeyboard()
        {
            _connectionsTree.KeyDown += (o, key) =>
            {
                if (key.KeyCode == (Key.N.KeyCode | KeyCode.CtrlMask))
                {
                    OnAddClicked();
                    key.Handled = true;
                }
                else if (key.KeyCode == (Key.E.KeyCode | KeyCode.CtrlMask))
                {
                    OnEditClicked();
                    key.Handled = true;
                }
                else if (key == Key.DeleteChar)
                {
                    OnDeleteClicked();
                    key.Handled = true;
                }
                else if (key == Key.Space)
                {
                    OnExpandDatabases();
                    key.Handled = true;
                }
            };
        }

        private void SetupLayout()
        {
            Add(_connectionsTree);
            _shortcutsLabels.ToList().ForEach(label => Add(label));
        }

        private async void LoadConnections()
        {
            try
            {
                var connections = await _connectionManager.GetConnectionsAsync();
                _connections = connections.ToArray();
                
                // Clear existing tree
                _connectionsTree.ClearObjects();
                
                // Add cluster nodes to tree
                foreach (var connection in _connections)
                {
                    var clusterNode = new ClusterTreeNode(connection, GetKustoClient(connection));
                    _connectionsTree.AddObject(clusterNode);
                }

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

        public async void RefreshConnections()
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
                    await clusterNode.LoadDatabasesAsync();
                    Application.Invoke(()=> _connectionsTree.RefreshObject(clusterNode));
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
                    AuthType = dbNode.ParentConnection.AuthType,
                    CreatedAt = dbNode.ParentConnection.CreatedAt,
                    LastUsed = DateTime.UtcNow,
                    IsDefault = dbNode.ParentConnection.IsDefault
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

        private IKustoClient GetKustoClient(KustoConnection connection)
        {
            if (!_kustoClients.TryGetValue(connection.Id, out var client))
            {
                var authProvider = AuthenticationProviderFactory.CreateProvider(connection.AuthType);
                client = new KustoClient(connection, authProvider);
                _kustoClients[connection.Id] = client;
            }

            return client;
        }
    }
}