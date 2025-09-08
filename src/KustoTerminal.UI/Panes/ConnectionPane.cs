using System;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Gui;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;
using KustoTerminal.UI.Dialogs;

namespace KustoTerminal.UI.Panes
{
    public class ConnectionPane : BasePane
    {
        private readonly IConnectionManager _connectionManager;
        private ListView _connectionsList;
        private Label _shortcutsLabel;
        
        private KustoConnection[] _connections = Array.Empty<KustoConnection>();
        private KustoConnection? _selectedConnection;

        public event EventHandler<KustoConnection>? ConnectionSelected;

        public ConnectionPane(IConnectionManager connectionManager)
        {
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            
            InitializeComponents();
            SetupLayout();
            SetupElementFocusHandlers();
            LoadConnections();
        }

        private void InitializeComponents()
        {
            _connectionsList = new ListView()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill() - 4,
                AllowsMarking = false,
                AllowsMultipleSelection = false
            };

            _shortcutsLabel = new Label("Ctrl+N: Add\nCtrl+E: Edit\nDel: Delete\nEnter: Connect")
            {
                X = 0,
                Y = Pos.Bottom(_connectionsList),
                Width = Dim.Fill(),
                Height = 1
            };
            
            // Apply color schemes using BasePane methods
            ApplyColorSchemeToControl(_shortcutsLabel, "shortcut");

            // Set up event handlers
            _connectionsList.SelectedItemChanged += OnConnectionSelectedChanged;
            _connectionsList.KeyPress += OnConnectionsListKeyPress;
        }

        private void SetupLayout()
        {
            Add(_connectionsList, _shortcutsLabel);
        }

        private void SetupElementFocusHandlers()
        {
            // Use BasePane's common focus handling for all controls
            SetupCommonElementFocusHandlers(_connectionsList, _shortcutsLabel);
        }

        private async void LoadConnections()
        {
            try
            {
                var connections = await _connectionManager.GetConnectionsAsync();
                _connections = connections.ToArray();
                
                var displayItems = _connections.Select(c => 
                    $"{(c.IsDefault ? "* " : "  ")}{c.DisplayName}").ToArray();
                
                _connectionsList.SetSource(displayItems);
                
                if (_connections.Length > 0)
                {
                    _connectionsList.SelectedItem = 0;
                    OnConnectionSelected();
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

        private void OnConnectionSelectedChanged(ListViewItemEventArgs args)
        {
            OnConnectionSelected();
        }

        private void OnConnectionSelected()
        {
            var selectedIndex = _connectionsList.SelectedItem;
            if (selectedIndex >= 0 && selectedIndex < _connections.Length)
            {
                _selectedConnection = _connections[selectedIndex];
                
                // Force a redraw to ensure selection highlighting is visible
                _connectionsList.SetNeedsDisplay();
            }
            else
            {
                _selectedConnection = null;
            }
        }

        private void OnConnectionsListKeyPress(KeyEventEventArgs args)
        {
            // Handle Enter key to connect to selected connection
            if (args.KeyEvent.Key == Key.Enter)
            {
                if (_selectedConnection != null)
                {
                    ConnectionSelected?.Invoke(this, _selectedConnection);
                    args.Handled = true;
                }
            }
            // Handle Ctrl+N for new connection
            else if (args.KeyEvent.Key == (Key.CtrlMask | Key.N))
            {
                OnAddClicked();
                args.Handled = true;
            }
            // Handle Ctrl+E for edit connection
            else if (args.KeyEvent.Key == (Key.CtrlMask | Key.E))
            {
                if (_selectedConnection != null)
                {
                    OnEditClicked();
                    args.Handled = true;
                }
            }
            // Handle Delete key for delete connection
            else if (args.KeyEvent.Key == Key.DeleteChar)
            {
                if (_selectedConnection != null)
                {
                    OnDeleteClicked();
                    args.Handled = true;
                }
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
                    Application.MainLoop.Invoke(() => RefreshConnections());
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
                    Application.MainLoop.Invoke(() => RefreshConnections());
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
                    Application.MainLoop.Invoke(() => RefreshConnections());
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
    }
}