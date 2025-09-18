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
        private bool _isLoadingDatabases = false;

        public ClusterTreeNode(KustoConnection connection, IKustoClient kustoClient)
            : base(GetDisplayText(connection))
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _kustoClient = kustoClient ?? throw new ArgumentNullException(nameof(kustoClient));
        }

        private static string GetDisplayText(KustoConnection connection)
        {
            var prefix = connection.IsDefault ? "* " : "  ";
            return $"{prefix}{connection.DisplayName}";
        }

        private string GetDisplayText()
        {
            var prefix = Connection.IsDefault ? "* " : "  ";
            var suffix = _isLoadingDatabases ? " (Loading...)" : "";
            return $"{prefix}{Connection.DisplayName}{suffix}";
        }

        public void SetLoadingState(bool isLoading)
        {
            _isLoadingDatabases = isLoading;
            Text = GetDisplayText();
        }

        public async Task LoadDatabasesAsync()
        {
            // if (_databasesLoaded)
            //     return;

            SetLoadingState(true);

            try
            {
                var databases = await _kustoClient.GetDatabasesAsync();
                
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