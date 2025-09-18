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

        public async Task<KustoConnection?> GetDefaultConnectionAsync()
        {
            await LoadConnectionsAsync();
            return _connections.FirstOrDefault(c => c.IsDefault) ?? _connections.FirstOrDefault();
        }

        public async Task AddConnectionAsync(KustoConnection connection)
        {
            await LoadConnectionsAsync();
            
            if (connection.IsDefault)
            {
                // Ensure only one default connection
                foreach (var conn in _connections)
                {
                    conn.IsDefault = false;
                }
            }
            
            _connections.Add(connection);
            await SaveConnectionsAsync();
        }

        public async Task UpdateConnectionAsync(KustoConnection connection)
        {
            await LoadConnectionsAsync();
            
            var existingIndex = _connections.FindIndex(c => c.Id == connection.Id);
            if (existingIndex >= 0)
            {
                if (connection.IsDefault)
                {
                    // Ensure only one default connection
                    foreach (var conn in _connections)
                    {
                        conn.IsDefault = false;
                    }
                }
                
                _connections[existingIndex] = connection;
                await SaveConnectionsAsync();
            }
        }

        public async Task DeleteConnectionAsync(string id)
        {
            await LoadConnectionsAsync();
            
            var connectionToRemove = _connections.FirstOrDefault(c => c.Id == id);
            if (connectionToRemove != null)
            {
                _connections.Remove(connectionToRemove);
                
                // If we removed the default connection, make the first one default
                if (connectionToRemove.IsDefault && _connections.Any())
                {
                    _connections.First().IsDefault = true;
                }
                
                await SaveConnectionsAsync();
            }
        }

        public async Task SetDefaultConnectionAsync(string id)
        {
            await LoadConnectionsAsync();
            
            foreach (var connection in _connections)
            {
                connection.IsDefault = connection.Id == id;
            }
            
            await SaveConnectionsAsync();
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