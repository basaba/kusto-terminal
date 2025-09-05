using System.Threading.Tasks;
using KustoTerminal.Core.Models;

namespace KustoTerminal.Core.Interfaces
{
    public interface IKustoClient
    {
        Task<QueryResult> ExecuteQueryAsync(string query);
        Task<bool> TestConnectionAsync();
        Task<string[]> GetDatabasesAsync();
        Task<string[]> GetTablesAsync(string database);
        void Dispose();
    }
}