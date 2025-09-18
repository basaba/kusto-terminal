using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Gui;
using KustoTerminal.Core.Models;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Services;
using Terminal.Gui.Views;

namespace KustoTerminal.UI.Models
{
    public class ClusterTreeNode : TreeNode
    {
        public KustoConnection Connection { get; }
        private readonly IKustoClient _kustoClient;
        private readonly IConnectionManager _connectionManager;
        private bool _isLoadingDatabases = false;

        public ClusterTreeNode(KustoConnection connection, IKustoClient kustoClient, IConnectionManager connectionManager)
            : base(GetDisplayText(connection))
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _kustoClient = kustoClient ?? throw new ArgumentNullException(nameof(kustoClient));
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            
            // Load persisted databases if available
            LoadPersistedDatabases();
        }

        private static string GetDisplayText(KustoConnection connection)
        {
            var prefix = connection.IsDefault ? "* " : "  ";
            return $"{prefix}{connection.DisplayName}";
        }

        private string GetDisplayText()
        {
            var prefix = Connection.IsDefault ? "* " : "  ";
            var suffix = _isLoadingDatabases ? " (Loading)" : "";
            return $"{prefix}{Connection.DisplayName}{suffix}";
        }

        public void SetLoadingState(bool isLoading)
        {
            _isLoadingDatabases = isLoading;
            Text = GetDisplayText();
        }

        private void LoadPersistedDatabases()
        {
            // Load from persisted databases if available
            if (Connection.Databases != null && Connection.Databases.Any())
            {
                Children.Clear();
                foreach (var database in Connection.Databases)
                {
                    Children.Add(new DatabaseTreeNode(database, Connection));
                }
            }
        }

        public async Task LoadDatabasesAsync(bool forceRefresh = false)
        {
            // If we have persisted databases and not forcing refresh, use them
            if (!forceRefresh && Connection.Databases != null && Connection.Databases.Any())
            {
                LoadPersistedDatabases();
                return;
            }

            SetLoadingState(true);

            try
            {
                var databases = await _kustoClient.GetDatabasesAsync();
                
                // Update the connection with fresh database list and persist it
                await _connectionManager.RefreshDatabasesAsync(Connection.Id, _kustoClient);
                
                // Clear existing children and add database nodes
                Children.Clear();
                foreach (var database in databases)
                {
                    Children.Add(new DatabaseTreeNode(database, Connection));
                }
            }
            catch (Exception ex)
            {
                // Add an error node if databases couldn't be loaded
                Children.Clear();
                Children.Add(new TreeNode($"Error loading databases: {ex.Message}"));
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        public async Task RefreshDatabasesAsync()
        {
            await LoadDatabasesAsync(forceRefresh: true);
        }

        public void RefreshDatabases()
        {
            Children.Clear();
        }
    }

    public class DatabaseTreeNode : TreeNode
    {
        public string DatabaseName { get; }
        public KustoConnection ParentConnection { get; }

        public DatabaseTreeNode(string databaseName, KustoConnection parentConnection)
            : base(databaseName)
        {
            DatabaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
            ParentConnection = parentConnection ?? throw new ArgumentNullException(nameof(parentConnection));
        }
    }
}