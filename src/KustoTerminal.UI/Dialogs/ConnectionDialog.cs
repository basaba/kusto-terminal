using System;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;

using KustoTerminal.Core.Models;
using Terminal.Gui.Input;

namespace KustoTerminal.UI.Dialogs
{
    public class ConnectionDialog : Dialog
    {
        private TextField _nameField = null!;
        private TextField _clusterUriField = null!;
        private TextField _databaseField = null!;
        private RadioGroup _authTypeGroup = null!;
        private Label _shortcutsLabel = null!;
        private readonly KustoConnection? _originalConnection;
        
        public KustoConnection? Result { get; private set; }

        public ConnectionDialog(KustoConnection? connection = null)
        {
            _originalConnection = connection;
            Title = connection == null ? "Add Connection" : "Edit Connection";
            Width = 60;
            Height = 18;

            InitializeComponents(connection);
            SetupLayout();
            SetKeyboard();
        }

        private void InitializeComponents(KustoConnection? connection)
        {
            // Name field
            var nameLabel = new Label()
            {
                Text = "Name:",
                X = 1,
                Y = 1
            };
            
            _nameField = new TextField()
            {
                Text = connection?.Name ?? "",
                X = 15,
                Y = 1,
                Width = 40
            };

            // Cluster URI field
            var clusterLabel = new Label()
            {
                Text = "Cluster URI:",
                X = 1,
                Y = 3
            };
            
            _clusterUriField = new TextField()
            {
                Text = connection?.ClusterUri ?? "",
                X = 15,
                Y = 3,
                Width = 40
            };

            // Database field
            var databaseLabel = new Label()
            {
                Text = "Database:",
                X = 1,
                Y = 5
            };
            
            _databaseField = new TextField()
            {
                Text = connection?.Database ?? "",
                X = 15,
                Y = 5,
                Width = 40
            };

            // Auth type selection
            var authTypeLabel = new Label()
            {
                Text = "Auth Type:",
                X = 1,
                Y = 7
            };

            _authTypeGroup = new RadioGroup()
            {
                X = 15,
                Y = 7,
                Width = 40,
                Height = 4,
                RadioLabels = new string[] { "None (Unauthenticated)", "Azure CLI" }
            };

            // Set initial auth type selection
            var authTypeIndex = connection?.AuthType switch
            {
                AuthenticationType.None => 0,
                AuthenticationType.AzureCli => 1,
                _ => 1 // Default to Azure CLI
            };
            _authTypeGroup.SelectedItem = authTypeIndex;

            // Shortcuts label
            _shortcutsLabel = new Label()
            {
                Text = "Enter: OK | Esc: Cancel",
                X = 1,
                Y = 12,
                Width = Dim.Fill()! - 2,
                Height = 1,
                // ColorScheme = ColorSchemeFactory.CreateShortcutLabel()
            };

            Add(nameLabel, _nameField, clusterLabel, _clusterUriField,
                databaseLabel, _databaseField, authTypeLabel, _authTypeGroup, _shortcutsLabel);
        }

        private void SetupLayout()
        {
            // Focus on the first field
            _nameField.SetFocus();
        }

        private void SetKeyboard()
        {
            KeyBindings.ReplaceCommands(Key.Enter, Command.Accept);
            AddCommand(Command.Accept, () => { OnOkClicked(); return true; });
            KeyBindings.ReplaceCommands(Key.Esc, Command.Cancel);
            AddCommand(Command.Cancel, () => { OnCancelClicked(); return true; });
        }

        private void OnOkClicked()
        {
            // Validate input
            var name = _nameField.Text?.ToString() ?? "";
            var clusterUri = _clusterUriField.Text?.ToString() ?? "";
            var database = _databaseField.Text?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(clusterUri))
            {
                MessageBox.ErrorQuery("Validation Error", "Cluster URI is required.", "OK");
                _clusterUriField.SetFocus();
                return;
            }

            if (string.IsNullOrWhiteSpace(database))
            {
                MessageBox.ErrorQuery("Validation Error", "Database is required.", "OK");
                _databaseField.SetFocus();
                return;
            }

            if (!Uri.TryCreate(clusterUri, UriKind.Absolute, out _))
            {
                MessageBox.ErrorQuery("Validation Error", "Invalid cluster URI format.", "OK");
                _clusterUriField.SetFocus();
                return;
            }

            // Get selected auth type
            var selectedAuthType = _authTypeGroup.SelectedItem switch
            {
                0 => AuthenticationType.None,
                1 => AuthenticationType.AzureCli,
                _ => AuthenticationType.AzureCli
            };

            // Create result - preserve original ID and timestamps for edits
            if (_originalConnection != null)
            {
                // Editing existing connection - preserve ID and metadata
                Result = new KustoConnection
                {
                    Id = _originalConnection.Id,
                    Name = name,
                    ClusterUri = clusterUri,
                    Database = database,
                    AuthType = selectedAuthType,
                };
            }
            else
            {
                // Creating new connection
                Result = new KustoConnection
                {
                    Name = name,
                    ClusterUri = clusterUri,
                    Database = database,
                    AuthType = selectedAuthType
                };
            }

            Application.RequestStop();
        }

        private void OnCancelClicked()
        {
            Result = null;
            Application.RequestStop();
        }
    }
}