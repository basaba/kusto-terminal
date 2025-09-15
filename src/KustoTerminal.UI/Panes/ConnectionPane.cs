using System;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;
using KustoTerminal.UI.Dialogs;
using Terminal.Gui.Input;
using System.Collections.ObjectModel;
using Terminal.Gui.Drivers;

namespace KustoTerminal.UI.Panes
{
    public class ConnectionPane : View
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
            SetKeyboard();
            LoadConnections();
            CanFocus = true;
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
                AllowsMultipleSelection = false,
            };

            _shortcutsLabel = new Label()
            {
                Text = "Enter: Connect\nCtrl+N: New\nCtrl+E: Edit\nDel: Delete",
                X = 0,
                Y = Pos.Bottom(_connectionsList),
                Width = Dim.Fill(),
                Height = 1
            };

            // Apply color schemes using BasePane methods
            // ApplyColorSchemeToControl(_shortcutsLabel, "shortcut");

            // Set up event handlers
            _connectionsList.SelectedItemChanged += OnConnectionSelectedChanged;
            _connectionsList.Accepting += (sender, args) => { OnConnectClicked(); args.Handled = true; };

        }

        private void SetKeyboard()
        {
            _connectionsList.KeyDown += (o, key) =>
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
            };
        }

        private void SetupLayout()
        {
            Add(_connectionsList, _shortcutsLabel);
        }

        private async void LoadConnections()
        {
            try
            {
                var connections = await _connectionManager.GetConnectionsAsync();
                _connections = connections.ToArray();
                
                var displayItems = _connections.Select(c => 
                    $"{(c.IsDefault ? "* " : "  ")}{c.DisplayName}").ToArray();

                _connectionsList.SetSource(new ObservableCollection<string>(displayItems));

                if (_connections.Length > 0)
                {
                    _connectionsList.SelectedItem = 0;
                    OnConnectionSelected(0);
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

        private void OnConnectionSelectedChanged(object? sender, ListViewItemEventArgs args)
        {
            OnConnectionSelected(args.Item);
        }

        private void OnConnectionSelected(int selectedIndex)
        {
            if (selectedIndex >= 0 && selectedIndex < _connections.Length)
            {
                _selectedConnection = _connections[selectedIndex];
                
                // Force a redraw to ensure selection highlighting is visible
                // _connectionsList.SetNeedsDisplay();
            }
            else
            {
                _selectedConnection = null;
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
    }
}