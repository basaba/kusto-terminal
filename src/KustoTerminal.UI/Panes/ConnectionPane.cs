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
        private Button _addButton;
        private Button _editButton;
        private Button _deleteButton;
        private Button _connectButton;
        
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
                Height = Dim.Fill() - 3,
                AllowsMarking = false,
                AllowsMultipleSelection = false
            };

            // Customize the ListView appearance for better selection highlighting
            SetupConnectionListStyle();

            _addButton = new Button("Add")
            {
                X = 0,
                Y = Pos.Bottom(_connectionsList),
                Width = 6
            };

            _editButton = new Button("Edit")
            {
                X = Pos.Right(_addButton) + 1,
                Y = Pos.Bottom(_connectionsList),
                Width = 6,
                Enabled = false
            };

            _deleteButton = new Button("Delete")
            {
                X = Pos.Right(_editButton) + 1,
                Y = Pos.Bottom(_connectionsList),
                Width = 8,
                Enabled = false
            };

            _connectButton = new Button("Connect")
            {
                X = 0,
                Y = Pos.Bottom(_addButton),
                Width = Dim.Fill(),
                Enabled = false
            };

            // Set up event handlers
            _connectionsList.SelectedItemChanged += OnConnectionSelectedChanged;
            _connectionsList.KeyPress += OnConnectionsListKeyPress;
            _addButton.Clicked += OnAddClicked;
            _editButton.Clicked += OnEditClicked;
            _deleteButton.Clicked += OnDeleteClicked;
            _connectButton.Clicked += OnConnectClicked;
        }

        private void SetupLayout()
        {
            Add(_connectionsList, _addButton, _editButton, _deleteButton, _connectButton);
        }

        private void SetupElementFocusHandlers()
        {
            // Set up focus handlers for individual elements
            _connectionsList.Enter += OnElementFocusEnter;
            _connectionsList.Leave += OnElementFocusLeave;
            _addButton.Enter += OnElementFocusEnter;
            _addButton.Leave += OnElementFocusLeave;
            _editButton.Enter += OnElementFocusEnter;
            _editButton.Leave += OnElementFocusLeave;
            _deleteButton.Enter += OnElementFocusEnter;
            _deleteButton.Leave += OnElementFocusLeave;
            _connectButton.Enter += OnElementFocusEnter;
            _connectButton.Leave += OnElementFocusLeave;
        }

        private void OnElementFocusEnter(FocusEventArgs args)
        {
            // When any element in this pane gets focus, highlight the entire pane
            SetHighlighted(true);
        }

        private void OnElementFocusLeave(FocusEventArgs args)
        {
            // Check if focus is moving to another element within this pane
            Application.MainLoop.Invoke(() =>
            {
                var focusedView = Application.Top.MostFocused;
                bool stillInPane = IsChildOf(focusedView, this);
                
                if (!stillInPane)
                {
                    SetHighlighted(false);
                }
            });
        }

        private bool IsChildOf(View? child, View parent)
        {
            if (child == null) return false;
            if (child == parent) return true;
            
            foreach (View subview in parent.Subviews)
            {
                if (IsChildOf(child, subview))
                    return true;
            }
            return false;
        }

        protected override void OnFocusEnter()
        {
            // Additional highlighting when the pane itself receives focus
            HighlightActiveElements();
        }

        protected override void OnFocusLeave()
        {
            // Remove highlighting when leaving the pane
            RemoveElementHighlighting();
        }

        private void HighlightActiveElements()
        {
            // Enhanced highlighting for the connections list when pane is active
            var highlightedListScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.BrightCyan, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow),
                Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
            };
            
            _connectionsList.ColorScheme = highlightedListScheme;
            _connectionsList.SetNeedsDisplay();
        }

        private void RemoveElementHighlighting()
        {
            // Reset to the original connection list style
            SetupConnectionListStyle();
        }

        private void SetupConnectionListStyle()
        {
            // Configure ListView color scheme for persistent selection highlighting
            // Selection remains visible even when focus is on other panes
            var normalListScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                HotNormal = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan), // Keep selection visible when not focused
                HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightYellow), // Brighter when focused
                Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
            };
            
            _connectionsList.ColorScheme = normalListScheme;
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
                _editButton.Enabled = true;
                _deleteButton.Enabled = true;
                _connectButton.Enabled = true;
                
                // Force a redraw to ensure selection highlighting is visible
                _connectionsList.SetNeedsDisplay();
            }
            else
            {
                _selectedConnection = null;
                _editButton.Enabled = false;
                _deleteButton.Enabled = false;
                _connectButton.Enabled = false;
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