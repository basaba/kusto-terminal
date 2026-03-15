using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kusto.Data.Common;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;
using KustoTerminal.Language.Services;

namespace KustoTerminal.Core.Services
{
    public class ClusterSchemaService
    {
        private LanguageService _languageService;
        private SchemaCacheManager? _cacheManager;

        public ClusterSchemaService(LanguageService languageService, CacheConfiguration? cacheConfiguration = null)
        {
            _languageService = languageService;
            
            // Initialize cache manager if configuration is provided
            if (cacheConfiguration != null && cacheConfiguration.EnableDiskCache)
            {
                try
                {
                    _cacheManager = new SchemaCacheManager(cacheConfiguration);
                    // Cleanup expired cache on initialization
                    _cacheManager.CleanupExpiredCache();
                }
                catch
                {
                    // If cache initialization fails, continue without caching
                    _cacheManager = null;
                }
            }
        }

        /// <summary>
        /// Fetches cluster schema and updates the language service for the given connection.
        /// For databases that use entity groups with no direct tables, a supplementary
        /// per-database schema fetch is performed to resolve actual table definitions.
        /// </summary>
        /// <param name="connection">The Kusto connection to fetch schema for</param>
        /// <param name="forceRefresh">If true, bypasses cache and fetches fresh schema</param>
        /// <returns>Task representing the async operation</returns>
        public async Task FetchAndUpdateClusterSchemaAsync(KustoConnection connection, bool forceRefresh = false)
        {
            if (connection == null || !connection.IsValid())
            {
                return;
            }

            var clusterName = connection.GetClusterNameFromUrl();
            ClusterSchema? clusterSchema = null;

            // Try to load from cache first if not forcing refresh
            if (!forceRefresh && _cacheManager != null)
            {
                try
                {
                    clusterSchema = await _cacheManager.GetCachedSchemaAsync(clusterName);
                }
                catch (Exception)
                {
                    // Cache retrieval failed, continue to fetch from server
                }
                if (clusterSchema != null)
                {
                    // If the connected database has entity groups but no tables, the cache
                    // is incomplete (predates backfill or backfill failed). Discard it and
                    // re-fetch so BackfillEntityGroupDatabasesAsync can resolve the tables.
                    if (NeedsEntityGroupBackfill(clusterSchema, connection.Database))
                    {
                        clusterSchema = null;
                    }
                    else
                    {
                        // Update language service with cached schema
                        _languageService.AddOrUpdateCluster(clusterName, clusterSchema);
                        return;
                    }
                }
            }

            // Fetch from server if no cache or force refresh
            var authProvider = AuthenticationProviderFactory.CreateProvider(connection.AuthType)!;
            var kustoClient = new KustoClient(connection, authProvider);

            try
            {
                clusterSchema = await kustoClient.GetClusterSchemaAsync();
                if (clusterSchema != null)
                {
                    // For databases that have entity groups but no tables, fetch the
                    // resolved database schema to get actual table definitions with columns.
                    await BackfillEntityGroupDatabasesAsync(kustoClient, clusterSchema, connection.Database);

                    // Update language service with the schema
                    _languageService.AddOrUpdateCluster(clusterName, clusterSchema);

                    // Save to cache
                    if (_cacheManager != null)
                    {
                        await _cacheManager.SaveSchemaToCacheAsync(clusterName, clusterSchema);
                    }
                }
            }
            catch { }
            finally
            {
                kustoClient?.Dispose();
            }
        }

        /// <summary>
        /// For the connected database, if it has entity groups but no tables, fetch
        /// the resolved database schema and merge the tables into the cluster schema.
        /// Tries .show database schema as json first, then falls back to getschema queries.
        /// </summary>
        private static async Task BackfillEntityGroupDatabasesAsync(
            KustoClient kustoClient, ClusterSchema clusterSchema, string connectedDatabase)
        {
            if (clusterSchema.Databases == null || string.IsNullOrEmpty(connectedDatabase))
                return;

            if (!clusterSchema.Databases.TryGetValue(connectedDatabase, out var dbSchema))
                return;

            if (!NeedsEntityGroupBackfill(dbSchema))
                return;

            // First try .show database schema as json (fast, single call)
            try
            {
                var resolvedSchema = await kustoClient.GetDatabaseSchemaAsync(connectedDatabase);
                if (resolvedSchema?.Tables != null && resolvedSchema.Tables.Count > 0)
                {
                    foreach (var table in resolvedSchema.Tables)
                    {
                        dbSchema.Tables ??= new Dictionary<string, TableSchema>();
                        dbSchema.Tables.TryAdd(table.Key, table.Value);
                    }
                }
                if (resolvedSchema?.MaterializedViews != null && resolvedSchema.MaterializedViews.Count > 0)
                {
                    foreach (var mv in resolvedSchema.MaterializedViews)
                    {
                        dbSchema.MaterializedViews ??= new Dictionary<string, TableSchema>();
                        dbSchema.MaterializedViews.TryAdd(mv.Key, mv.Value);
                    }
                }
            }
            catch { }

            // If we still have no tables, fall back to getschema queries for shortcut functions.
            // These go through the query engine which resolves entity group references.
            if (dbSchema.Tables == null || dbSchema.Tables.Count == 0)
            {
                await BackfillViaGetSchemaAsync(kustoClient, dbSchema, connectedDatabase);
            }
        }

        /// <summary>
        /// Uses getschema queries to resolve column schemas for shortcut functions.
        /// </summary>
        private static async Task BackfillViaGetSchemaAsync(
            KustoClient kustoClient, DatabaseSchema dbSchema, string databaseName)
        {
            if (dbSchema.Functions == null || dbSchema.Functions.Count == 0)
                return;

            // Collect shortcut function names (these represent tables)
            var shortcutNames = new List<string>();
            foreach (var funcKvp in dbSchema.Functions)
            {
                var fs = funcKvp.Value;
                if (fs.Folder == "Shortcuts"
                    && (fs.InputParameters == null || fs.InputParameters.Count == 0))
                {
                    shortcutNames.Add(funcKvp.Key);
                }
            }

            if (shortcutNames.Count == 0)
                return;

            try
            {
                var schemas = await kustoClient.GetTableSchemasViaQueryAsync(databaseName, shortcutNames);

                dbSchema.Tables ??= new Dictionary<string, TableSchema>();
                foreach (var kvp in schemas)
                {
                    var columns = kvp.Value
                        .Select(col => new ColumnSchema(col.Name, col.DataType, null, col.CslType))
                        .ToList();

                    var tableSchema = new TableSchema(kvp.Key, columns);
                    dbSchema.Tables.TryAdd(kvp.Key, tableSchema);
                }
            }
            catch { }
        }

        /// <summary>
        /// Checks whether a cluster schema's connected database needs entity group backfill.
        /// </summary>
        private static bool NeedsEntityGroupBackfill(ClusterSchema clusterSchema, string connectedDatabase)
        {
            if (clusterSchema.Databases == null || string.IsNullOrEmpty(connectedDatabase))
                return false;
            if (!clusterSchema.Databases.TryGetValue(connectedDatabase, out var dbSchema))
                return false;
            return NeedsEntityGroupBackfill(dbSchema);
        }

        /// <summary>
        /// A database needs backfill when it has entity groups but no tables.
        /// </summary>
        private static bool NeedsEntityGroupBackfill(DatabaseSchema dbSchema)
        {
            bool hasNoTables = dbSchema.Tables == null || dbSchema.Tables.Count == 0;
            bool hasEntityGroups = dbSchema.EntityGroups != null && dbSchema.EntityGroups.Count > 0;
            return hasNoTables && hasEntityGroups;
        }

        /// <summary>
        /// Fetches cluster schemas for multiple connections concurrently
        /// </summary>
        /// <param name="connections">The connections to fetch schemas for</param>
        /// <param name="forceRefresh">If true, bypasses cache and fetches fresh schemas</param>
        /// <returns>Task representing the async operation</returns>
        public async Task FetchAndUpdateMultipleClusterSchemasAsync(IEnumerable<KustoConnection> connections, bool forceRefresh = false)
        {
            var tasks = new List<Task>();
            foreach (var connection in connections)
            {
                // Launch each schema fetch as a separate task
                tasks.Add(FetchAndUpdateClusterSchemaAsync(connection, forceRefresh));
            }
            
            await Task.WhenAll(tasks);
        }
    }
}
