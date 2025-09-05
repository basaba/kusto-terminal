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
        
        public KustoConnection? Result { get; private set; }

        public ConnectionDialog(KustoConnection? connection = null)
        {
            Title = connection == null ? "Add Connection" : "Edit Connection";
            Width = 60;
            Height = 15;
            
            InitializeComponents(connection);
            SetupLayout();
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

            // Create result
            Result = new KustoConnection
            {
                Name = name,
                ClusterUri = clusterUri,
                Database = database,
                IsDefault = _isDefaultCheckBox.Checked,
                AuthType = AuthenticationType.AzureCli
            };

            Application.RequestStop();
        }

        private void OnCancelClicked()
        {
            Result = null;
            Application.RequestStop();
        }
    }
}