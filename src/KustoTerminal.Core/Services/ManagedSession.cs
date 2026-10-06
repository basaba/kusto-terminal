using Kusto.Data;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;

namespace KustoTerminal.Core.Services;

/// <summary>One immutable target; no persisted or retained query, history, connection or settings state.</summary>
public sealed class ManagedSession : IAuthenticationProvider, IConnectionManager, IUserSettingsManager,
    IExecutionHistoryManager, IDisposable
{
    private readonly ManagedShellOptions _options;
    private readonly ManagedTokenChannel _channel;
    private readonly string _connectionId = Guid.NewGuid().ToString();
    public ManagedSession(ManagedTokenChannel channel)
        => (_options, _channel) = (channel.Options, channel);

    public KustoConnection CreateConnection() => new()
    {
        Id = _connectionId, Name = _options.ClusterUri, ClusterUri = _options.ClusterUri,
        Database = _options.Database, Databases = [_options.Database], AuthType = AuthenticationType.ManagedShell
    };

    public void ValidateTarget(KustoConnection connection, string? database = null)
    {
        if (connection.ClusterUri != _options.ClusterUri || connection.Database != _options.Database ||
            connection.AuthType != AuthenticationType.ManagedShell ||
            (database != null && database != _options.Database))
            throw new InvalidOperationException("The managed shell target cannot be changed.");
    }

    public async Task<KustoConnectionStringBuilder> CreateConnectionStringBuilderAsync(KustoConnection connection,
        CancellationToken cancellationToken = default)
    {
        ValidateTarget(connection);
        var credential = await _channel.AcquireAsync(cancellationToken);
        var builder = new KustoConnectionStringBuilder(_options.ClusterUri, _options.Database);
        return credential.Scheme == ManagedTokenScheme.Msal
            ? builder.WithAadUserTokenAuthentication(credential.AccessToken)
            : builder.WithDstsUserTokenAuthentication(credential.AccessToken);
    }

    public AuthenticationType AuthType => AuthenticationType.ManagedShell;
    public bool IsAuthenticated() => false;
    public Task<bool> ValidateAuthenticationAsync() => Task.FromResult(false);
    public Task<string> GetAccessTokenAsync(string clusterUri) =>
        throw new InvalidOperationException("Managed credentials require an explicit SDK token scheme.");
    public event EventHandler<KustoConnection>? ConnectionAddOrUpdated { add { } remove { } }
    public Task<IEnumerable<KustoConnection>> GetConnectionsAsync() =>
        Task.FromResult<IEnumerable<KustoConnection>>([CreateConnection()]);
    public Task<KustoConnection?> GetConnectionAsync(string id) =>
        Task.FromResult(id == _connectionId ? CreateConnection() : null);
    public Task AddConnectionAsync(KustoConnection connection) => throw FixedTarget();
    public Task UpdateConnectionAsync(KustoConnection connection) => throw FixedTarget();
    public Task DeleteConnectionAsync(string id) => throw FixedTarget();
    private static InvalidOperationException FixedTarget() => new("The managed shell target cannot be changed.");
    public Task RefreshDatabasesAsync(string connectionId, IKustoClient kustoClient) => Task.CompletedTask;
    public Task SaveConnectionsAsync() => Task.CompletedTask;
    public Task LoadConnectionsAsync() => Task.CompletedTask;
    public Task<UserSettings> LoadSettingsAsync() => Task.FromResult(new UserSettings());
    public Task SaveSettingsAsync(UserSettings settings) => Task.CompletedTask;
    public Task SaveLastQueryAsync(string query) => Task.CompletedTask;
    public Task<string> GetLastQueryAsync() => Task.FromResult("");
    public Task SaveTabsAsync(List<TabState> tabs) => Task.CompletedTask;
    public Task<List<TabState>> GetTabsAsync() => Task.FromResult(new List<TabState>());
    public Task AppendAsync(KustoConnection connection, string query, QueryResult result,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<QueryResult?> GetLatestAsync(KustoConnection connection, string query,
        CancellationToken cancellationToken = default) => Task.FromResult<QueryResult?>(null);
    public void Dispose() => _channel.Dispose();
}
