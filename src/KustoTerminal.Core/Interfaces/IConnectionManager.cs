using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KustoTerminal.Core.Models;

namespace KustoTerminal.Core.Interfaces
{
    public interface IConnectionManager
    {
        /// <summary>
        /// Event raised when a connection is updated
        /// </summary>
        event EventHandler<KustoConnection>? ConnectionAddOrUpdated;
        
        Task<IEnumerable<KustoConnection>> GetConnectionsAsync();
        Task<KustoConnection?> GetConnectionAsync(string id);
        Task AddConnectionAsync(KustoConnection connection);
        Task UpdateConnectionAsync(KustoConnection connection);
        Task DeleteConnectionAsync(string id);
        Task RefreshDatabasesAsync(string connectionId, IKustoClient kustoClient);
        Task SaveConnectionsAsync();
        Task LoadConnectionsAsync();
    }
}
