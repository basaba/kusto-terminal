using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;

namespace KustoTerminal.Core.Services
{
    public class ConnectionManager : IConnectionManager
    {
        private readonly string _configFilePath;
        private List<KustoConnection> _connections;
        
        /// <summary>
        /// Event raised when a connection is updated
        /// </summary>
        public event EventHandler<KustoConnection>? ConnectionAddOrUpdated;

        public ConnectionManager()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "KustoTerminal");
            Directory.CreateDirectory(appFolder);
            _configFilePath = Path.Combine(appFolder, "connections.json");
            _connections = new List<KustoConnection>();
        }

        public async Task<IEnumerable<KustoConnection>> GetConnectionsAsync()
        {
            await LoadConnectionsAsync();
            return _connections.AsReadOnly();
        }

        public async Task<KustoConnection?> GetConnectionAsync(string id)
        {
            await LoadConnectionsAsync();
            return _connections.FirstOrDefault(c => c.Id == id);
        }

        public async Task AddConnectionAsync(KustoConnection connection)
        {
            await LoadConnectionsAsync();
            
            _connections.Add(connection);
            await SaveConnectionsAsync();

            // Raise the ConnectionUpdated event
            ConnectionAddOrUpdated?.Invoke(this, connection);
        }

        public async Task UpdateConnectionAsync(KustoConnection connection)
        {
            await LoadConnectionsAsync();
            
            var existingIndex = _connections.FindIndex(c => c.Id == connection.Id);
            if (existingIndex >= 0)
            {
                _connections[existingIndex] = connection;
                await SaveConnectionsAsync();
                
                // Raise the ConnectionUpdated event
                ConnectionAddOrUpdated?.Invoke(this, connection);
            }
        }

        public async Task DeleteConnectionAsync(string id)
        {
            await LoadConnectionsAsync();
            
            var connectionToRemove = _connections.FirstOrDefault(c => c.Id == id);
            if (connectionToRemove != null)
            {
                _connections.Remove(connectionToRemove);
                await SaveConnectionsAsync();
            }
        }

        public async Task RefreshDatabasesAsync(string connectionId, IKustoClient kustoClient)
        {
            await LoadConnectionsAsync();
            
            var connection = _connections.FirstOrDefault(c => c.Id == connectionId);
            if (connection != null)
            {
                try
                {
                    var databases = await kustoClient.GetDatabasesAsync();
                    connection.Databases = databases.ToList();
                    await SaveConnectionsAsync();
                }
                catch
                {
                    // If we can't refresh databases, keep the existing list
                    // This could happen due to network issues or permissions
                }
            }
        }

        public async Task SaveConnectionsAsync()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_connections, Formatting.Indented);
                await File.WriteAllTextAsync(_configFilePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save connections: {ex.Message}", ex);
            }
        }

        public async Task LoadConnectionsAsync()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = await File.ReadAllTextAsync(_configFilePath);
                    var connections = JsonConvert.DeserializeObject<List<KustoConnection>>(json);
                    _connections = connections ?? new List<KustoConnection>();
                }
                else
                {
                    _connections = new List<KustoConnection>();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load connections: {ex.Message}", ex);
            }
        }
    }
}
