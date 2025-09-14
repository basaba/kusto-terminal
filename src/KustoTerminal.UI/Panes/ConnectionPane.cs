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
            SetupElementFocusHandlers();
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
                AllowsMultipleSelection = false
            };

            _shortcutsLabel = new Label()
            {
                Text = "Enter: Connect | Ctrl+N: New | Ctrl+E: Edit | Del: Delete",
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
            // _connectionsList.KeyPress += OnConnectionsListKeyPress;
            // AddCommand(Command.Accept, () => { OnConnectClicked(); return true; });
            //KeyBindings.Add(Key)
            //.ReplaceCommands(Key.Enter, Command.Select);

            _connectionsList.CanFocus = true; ;
            _connectionsList.KeyDown += (sender, key) =>
            {
                if (key == Key.A)
                {
                    OnAddClicked();
                }
            };
        }

        private void SetupLayout()
        {
            Add(_connectionsList, _shortcutsLabel);
        }

        private void SetupElementFocusHandlers()
        {
            // Use BasePane's common focus handling for all controls
            // SetupCommonElementFocusHandlers(_connectionsList, _shortcutsLabel);
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
            // OnConnectClicked();
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

        // private void OnConnectionsListKeyPress(KeyEventEventArgs args)
        // {
        //     // Handle Enter key to connect to selected connection
        //     if (args.KeyEvent.Key == Key.Enter)
        //     {
        //         if (_selectedConnection != null)
        //         {
        //             ConnectionSelected?.Invoke(this, _selectedConnection);
        //             args.Handled = true;
        //         }
        //     }
        //     // Handle Ctrl+N for new connection
        //     else if (args.KeyEvent.Key == (Key.CtrlMask | Key.N))
        //     {
        //         OnAddClicked();
        //         args.Handled = true;
        //     }
        //     // Handle Ctrl+E for edit connection
        //     else if (args.KeyEvent.Key == (Key.CtrlMask | Key.E))
        //     {
        //         if (_selectedConnection != null)
        //         {
        //             OnEditClicked();
        //             args.Handled = true;
        //         }
        //     }
        //     // Handle Delete key for delete connection
        //     else if (args.KeyEvent.Key == Key.DeleteChar)
        //     {
        //         if (_selectedConnection != null)
        //         {
        //             OnDeleteClicked();
        //             args.Handled = true;
        //         }
        //     }
        // }

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