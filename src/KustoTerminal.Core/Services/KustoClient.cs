using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;

namespace KustoTerminal.Core.Services;

public class KustoClient : IKustoClient
{
    private readonly KustoConnection _connection;
    private readonly IAuthenticationProvider _authProvider;
    private ICslQueryProvider? _queryProvider;
    private ICslAdminProvider? _adminProvider;
    private string? _currentRequestId;
    private CancellationTokenSource? _internalCancellationSource;

    public KustoClient(KustoConnection connection, IAuthenticationProvider authProvider)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _authProvider = authProvider ?? throw new ArgumentNullException(nameof(authProvider));
    }

    public async Task<QueryResult> ExecuteQueryAsync(string query, CancellationToken cancellationToken = default, IProgress<string>? progress = null)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Clean up any previous internal cancellation source
        _internalCancellationSource?.Dispose();
        _internalCancellationSource = new CancellationTokenSource();
        
        // Combine external cancellation token with internal one
        var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _internalCancellationSource.Token);
        var combinedToken = combinedCancellationSource.Token;
        
        try
        {
            progress?.Report("Initializing connection...");
            await EnsureConnectionAsync();
            
            // Check for cancellation after connection setup
            combinedToken.ThrowIfCancellationRequested();
            
            // Determine if this is a command (starts with dot) or a query
            var isCommand = query.TrimStart().StartsWith(".");
            
            if (isCommand && _adminProvider == null)
                throw new InvalidOperationException("Admin provider is not initialized");
            else if (!isCommand && _queryProvider == null)
                throw new InvalidOperationException("Query provider is not initialized");

            progress?.Report(isCommand ? "Preparing command..." : "Preparing query...");
            var clientRequestProperties = new ClientRequestProperties();

            // Always set up request ID for potential cancellation
            _currentRequestId = $"KustoTerminal;{Guid.NewGuid().ToString()}";
            clientRequestProperties.ClientRequestId = _currentRequestId;
            
            // Set up cancellation monitoring
            var cancellationMonitorTask = MonitorCancellationAsync(combinedToken, progress);
            
            // Check for cancellation before executing query/command
            combinedToken.ThrowIfCancellationRequested();
            
            progress?.Report(isCommand ? "Executing command..." : "Executing query...");
            
            // Execute query or command based on whether it starts with a dot
            IDataReader reader;
            if (isCommand)
            {
                reader = await _adminProvider!.ExecuteControlCommandAsync(_connection.Database, query, clientRequestProperties);
            }
            else
            {
                reader = await _queryProvider!.ExecuteQueryAsync(_connection.Database, query, clientRequestProperties);
            }
            
            // Check for cancellation after query/command execution
            combinedToken.ThrowIfCancellationRequested();
            
            progress?.Report("Processing results...");
            var dataTable = new DataTable();
            
            // Load data with cancellation checks
            await Task.Run(() =>
            {
                dataTable.Load(reader);
            }, combinedToken);
            
            progress?.Report(isCommand ? "Command completed successfully" : "Query completed successfully");
            stopwatch.Stop();
            return QueryResult.Success(query, dataTable, stopwatch.Elapsed, _currentRequestId);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            progress?.Report("Cancelling query on server...");
            
            // Cancel the query on the server side using .cancel query command
            await CancelQueryOnServerAsync();
            
            progress?.Report("Query cancelled");
            return QueryResult.Error(query, "Query was cancelled", stopwatch.Elapsed, _currentRequestId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            progress?.Report($"Query failed: {ex.Message}");
            return QueryResult.Error(query, ex.Message, stopwatch.Elapsed, _currentRequestId);
        }
        finally
        {
            combinedCancellationSource?.Dispose();
            _currentRequestId = null;
        }
    }

    private async Task MonitorCancellationAsync(CancellationToken cancellationToken, IProgress<string>? progress)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // When cancellation is requested, immediately try to cancel on server
            progress?.Report("Cancellation requested, stopping query...");
            await CancelQueryOnServerAsync();
        }
    }

    private async Task CancelQueryOnServerAsync()
    {
        if (string.IsNullOrEmpty(_currentRequestId) || _adminProvider == null)
            return;

        try
        {
            var cancelCommand = $".cancel query '{_currentRequestId}'";
            var cancelProperties = new ClientRequestProperties();
            
            // Execute the cancel command with a short timeout
            await _adminProvider.ExecuteControlCommandAsync(_connection.Database, cancelCommand, cancelProperties);
        }
        catch
        {
            // Ignore errors when trying to cancel - the query might have already completed
            // or there might be permission issues
        }
    }

    public Task CancelCurrentQueryAsync()
    {
        if (_internalCancellationSource != null && !_internalCancellationSource.Token.IsCancellationRequested)
        {
            _internalCancellationSource.Cancel();
        }
        
        // Fire-and-forget server cancellation to avoid blocking
        _ = Task.Run(async () =>
        {
            try
            {
                await CancelQueryOnServerAsync();
            }
            catch (Exception ex)
            {
                // Log but don't throw - cancellation errors shouldn't affect anything
                Console.WriteLine($"Warning: Server-side query cancellation failed: {ex.Message}");
            }
        });
        
        return Task.CompletedTask;
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await EnsureConnectionAsync();
            
            if (_queryProvider == null)
                return false;

            // Simple test query
            var testQuery = ".show version";
            var clientRequestProperties = new ClientRequestProperties();
            using var reader = await _queryProvider.ExecuteQueryAsync(_connection.Database, testQuery, clientRequestProperties);
            
            return reader != null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string[]> GetDatabasesAsync()
    {
        try
        {
            await EnsureConnectionAsync();
            
            if (_adminProvider == null)
                throw new InvalidOperationException("Admin provider is not initialized");

            var query = ".show databases";
            var clientRequestProperties = new ClientRequestProperties();
            using var reader = await _adminProvider.ExecuteControlCommandAsync(_connection.Database, query, clientRequestProperties);
            
            var databases = new List<string>();
            while (reader.Read())
            {
                if (reader["DatabaseName"] != null)
                {
                    databases.Add(reader["DatabaseName"].ToString() ?? string.Empty);
                }
            }
            
            return databases.ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public async Task<string[]> GetTablesAsync(string database)
    {
        try
        {
            await EnsureConnectionAsync();
            
            if (_queryProvider == null)
                throw new InvalidOperationException("Query provider is not initialized");

            var query = ".show tables";
            var clientRequestProperties = new ClientRequestProperties();
            using var reader = await _queryProvider.ExecuteQueryAsync(database, query, clientRequestProperties);
            
            var tables = new List<string>();
            while (reader.Read())
            {
                if (reader["TableName"] != null)
                {
                    tables.Add(reader["TableName"].ToString() ?? string.Empty);
                }
            }
            
            return tables.ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private async Task EnsureConnectionAsync()
    {
        if (_queryProvider != null && _adminProvider != null)
            return;

        KustoConnectionStringBuilder connectionStringBuilder;
        
        if (_connection.AuthType == AuthenticationType.None)
        {
            // For unauthenticated connections, create a connection string without authentication
            connectionStringBuilder = new KustoConnectionStringBuilder(_connection.ClusterUri);
        }
        else
        {
            // For authenticated connections, get token and use it
            var token = await _authProvider.GetAccessTokenAsync(_connection.ClusterUri);
            connectionStringBuilder = new KustoConnectionStringBuilder(_connection.ClusterUri)
                .WithAadUserTokenAuthentication(token);
        }

        _queryProvider = KustoClientFactory.CreateCslQueryProvider(connectionStringBuilder);
        _adminProvider = KustoClientFactory.CreateCslAdminProvider(connectionStringBuilder);
    }

    public void Dispose()
    {
        _queryProvider?.Dispose();
        _adminProvider?.Dispose();
    }
}