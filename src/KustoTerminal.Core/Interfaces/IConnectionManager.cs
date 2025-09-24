using System.Collections.Generic;
using System.Threading.Tasks;
using KustoTerminal.Core.Models;

namespace KustoTerminal.Core.Interfaces;

public interface IConnectionManager
{
    Task<IEnumerable<KustoConnection>> GetConnectionsAsync();
    Task<KustoConnection?> GetConnectionAsync(string id);
    Task<KustoConnection?> GetDefaultConnectionAsync();
    Task AddConnectionAsync(KustoConnection connection);
    Task UpdateConnectionAsync(KustoConnection connection);
    Task DeleteConnectionAsync(string id);
    Task SetDefaultConnectionAsync(string id);
    Task RefreshDatabasesAsync(string connectionId, IKustoClient kustoClient);
    Task SaveConnectionsAsync();
    Task LoadConnectionsAsync();
}