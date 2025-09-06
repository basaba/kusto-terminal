using System;
using Terminal.Gui;
using KustoTerminal.Core.Models;

namespace KustoTerminal.UI.Dialogs
{
    public class ConnectionDialog : Dialog
    {
        private TextField _nameField;
        private TextField _clusterUriField;
        private TextField _databaseField;
        private CheckBox _isDefaultCheckBox;
        private Button _okButton;
        private Button _cancelButton;
        private readonly KustoConnection? _originalConnection;
        
        public KustoConnection? Result { get; private set; }

        public ConnectionDialog(KustoConnection? connection = null)
        {
            _originalConnection = connection;
            Title = connection == null ? "Add Connection" : "Edit Connection";
            Width = 60;
            Height = 15;
            
            InitializeComponents(connection);
            SetupLayout();
            SetupColorScheme();
        }

        private void InitializeComponents(KustoConnection? connection)
        {
            // Name field
            var nameLabel = new Label("Name:")
            {
                X = 1,
                Y = 1
            };
            
            _nameField = new TextField(connection?.Name ?? "")
            {
                X = 15,
                Y = 1,
                Width = 40
            };

            // Cluster URI field
            var clusterLabel = new Label("Cluster URI:")
            {
                X = 1,
                Y = 3
            };
            
            _clusterUriField = new TextField(connection?.ClusterUri ?? "")
            {
                X = 15,
                Y = 3,
                Width = 40
            };

            // Database field
            var databaseLabel = new Label("Database:")
            {
                X = 1,
                Y = 5
            };
            
            _databaseField = new TextField(connection?.Database ?? "")
            {
                X = 15,
                Y = 5,
                Width = 40
            };

            // Default checkbox
            _isDefaultCheckBox = new CheckBox("Set as default connection")
            {
                X = 1,
                Y = 7,
                Checked = connection?.IsDefault ?? false
            };

            // Buttons
            _okButton = new Button("OK")
            {
                X = 10,
                Y = 10,
                Width = 10,
                IsDefault = true
            };

            _cancelButton = new Button("Cancel")
            {
                X = 25,
                Y = 10,
                Width = 10
            };

            // Event handlers
            _okButton.Clicked += OnOkClicked;
            _cancelButton.Clicked += OnCancelClicked;

            Add(nameLabel, _nameField, clusterLabel, _clusterUriField, 
                databaseLabel, _databaseField, _isDefaultCheckBox, 
                _okButton, _cancelButton);
        }

        private void SetupLayout()
        {
            // Focus on the first field
            _nameField.SetFocus();
        }

        private void SetupColorScheme()
        {
            // Set up consistent black background color scheme for the dialog
            var dialogColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.White, Color.Black),
                HotNormal = new Terminal.Gui.Attribute(Color.BrightBlue, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.BrightBlue, Color.Black),
                Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
            };

            // Apply to all components
            ColorScheme = dialogColorScheme;

            // Set up text field color schemes
            var textFieldColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                HotNormal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
            };

            _nameField.ColorScheme = textFieldColorScheme;
            _clusterUriField.ColorScheme = textFieldColorScheme;
            _databaseField.ColorScheme = textFieldColorScheme;

            // Set up button color schemes
            var buttonColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightBlue),
                HotNormal = new Terminal.Gui.Attribute(Color.BrightBlue, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
            };

            _okButton.ColorScheme = buttonColorScheme;
            _cancelButton.ColorScheme = buttonColorScheme;

            // Set up checkbox color scheme
            var checkboxColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(Color.White, Color.Black),
                Focus = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                HotNormal = new Terminal.Gui.Attribute(Color.BrightBlue, Color.Black),
                HotFocus = new Terminal.Gui.Attribute(Color.Black, Color.BrightCyan),
                Disabled = new Terminal.Gui.Attribute(Color.DarkGray, Color.Black)
            };

            _isDefaultCheckBox.ColorScheme = checkboxColorScheme;
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
                    IsDefault = _isDefaultCheckBox.Checked,
                    AuthType = _originalConnection.AuthType,
                    CreatedAt = _originalConnection.CreatedAt,
                    LastUsed = _originalConnection.LastUsed
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
                    IsDefault = _isDefaultCheckBox.Checked,
                    AuthType = AuthenticationType.AzureCli
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