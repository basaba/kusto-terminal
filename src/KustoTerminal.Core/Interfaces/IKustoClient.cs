using System;
using System.Threading;
using System.Threading.Tasks;
using KustoTerminal.Core.Models;

namespace KustoTerminal.Core.Interfaces
{
    public interface IKustoClient
    {
        Task<QueryResult> ExecuteQueryAsync(string query, CancellationToken cancellationToken = default, IProgress<string>? progress = null);
        Task<bool> TestConnectionAsync();
        Task<string[]> GetDatabasesAsync();
        Task<string[]> GetTablesAsync(string database);
        void Dispose();
    }
}