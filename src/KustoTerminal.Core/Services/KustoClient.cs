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

namespace KustoTerminal.Core.Services
{
    public class KustoClient : IKustoClient
    {
        private readonly KustoConnection _connection;
        private readonly IAuthenticationProvider _authProvider;
        private ICslQueryProvider? _queryProvider;
        private ICslAdminProvider? _adminProvider;

        public KustoClient(KustoConnection connection, IAuthenticationProvider authProvider)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _authProvider = authProvider ?? throw new ArgumentNullException(nameof(authProvider));
        }

        public async Task<QueryResult> ExecuteQueryAsync(string query, CancellationToken cancellationToken = default, IProgress<string>? progress = null)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                progress?.Report("Initializing connection...");
                await EnsureConnectionAsync();
                
                if (_queryProvider == null)
                    throw new InvalidOperationException("Query provider is not initialized");

                progress?.Report("Preparing query...");
                var clientRequestProperties = new ClientRequestProperties();
                
                // Add cancellation token support
                if (cancellationToken.CanBeCanceled)
                {
                    clientRequestProperties.ClientRequestId = Guid.NewGuid().ToString();
                }
                
                progress?.Report("Executing query...");
                var reader = await _queryProvider.ExecuteQueryAsync(_connection.Database, query, clientRequestProperties);
                
                progress?.Report("Processing results...");
                var dataTable = new DataTable();
                dataTable.Load(reader);
                
                progress?.Report("Query completed successfully");
                stopwatch.Stop();
                return QueryResult.Success(query, dataTable, stopwatch.Elapsed);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                progress?.Report("Query cancelled");
                return QueryResult.Error(query, "Query was cancelled", stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                progress?.Report($"Query failed: {ex.Message}");
                return QueryResult.Error(query, ex.Message, stopwatch.Elapsed);
            }
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

            var token = await _authProvider.GetAccessTokenAsync(_connection.ClusterUri);
            var connectionStringBuilder = new KustoConnectionStringBuilder(_connection.ClusterUri)
                .WithAadUserTokenAuthentication(token);

            _queryProvider = KustoClientFactory.CreateCslQueryProvider(connectionStringBuilder);
            _adminProvider = KustoClientFactory.CreateCslAdminProvider(connectionStringBuilder);
        }

        public void Dispose()
        {
            _queryProvider?.Dispose();
            _adminProvider?.Dispose();
        }
    }
}