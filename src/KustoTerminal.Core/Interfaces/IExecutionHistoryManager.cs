using KustoTerminal.Core.Models;

namespace KustoTerminal.Core.Interfaces;

public interface IExecutionHistoryManager
{
    Task AppendAsync(
        KustoConnection connection,
        string query,
        QueryResult result,
        CancellationToken cancellationToken = default);

    Task<QueryResult?> GetLatestAsync(
        KustoConnection connection,
        string query,
        CancellationToken cancellationToken = default);
}
