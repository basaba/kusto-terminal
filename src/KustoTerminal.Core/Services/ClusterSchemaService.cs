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
                    // is incomplete. Discard it and re-fetch so backfill can resolve tables.
                    if (NeedsEntityGroupBackfill(clusterSchema, connection.Database))
                    {
                        clusterSchema = null;
                    }
                    else
                    {
                        // Update language service with cached schema
                        _languageService.AddOrUpdateCluster(clusterName, clusterSchema);

                        // If functions still have macro-expand bodies, backfill them in background
                        if (NeedsFunctionBodyBackfill(clusterSchema, connection.Database))
                        {
                            var bgAuth = AuthenticationProviderFactory.CreateProvider(connection.AuthType)!;
                            var bgClient = new KustoClient(connection, bgAuth);
                            _ = BackfillFunctionBodiesInBackgroundAsync(
                                bgClient, clusterSchema, connection.Database, clusterName);
                        }
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
                    await BackfillEntityGroupTablesAsync(kustoClient, clusterSchema, connection.Database);

                    // Update language service with the schema (tables + basic functions)
                    _languageService.AddOrUpdateCluster(clusterName, clusterSchema);

                    // Save to cache
                    if (_cacheManager != null)
                    {
                        await _cacheManager.SaveSchemaToCacheAsync(clusterName, clusterSchema);
                    }

                    // Resolve function output schemas in the background so it doesn't
                    // block initial autocomplete. When done, update the language service
                    // and cache again with the enriched function bodies.
                    _ = BackfillFunctionBodiesInBackgroundAsync(
                        kustoClient, clusterSchema, connection.Database, clusterName);
                }
            }
            catch { }
        }

        /// <summary>
        /// For the connected database, if it has entity groups but no tables, fetch
        /// the resolved table schemas and merge them into the cluster schema.
        /// </summary>
        private static async Task BackfillEntityGroupTablesAsync(
            KustoClient kustoClient, ClusterSchema clusterSchema, string connectedDatabase)
        {
            if (clusterSchema.Databases == null || string.IsNullOrEmpty(connectedDatabase))
                return;

            if (!clusterSchema.Databases.TryGetValue(connectedDatabase, out var dbSchema))
                return;

            if (!NeedsEntityGroupBackfill(dbSchema))
                return;

            await BackfillTablesAsync(kustoClient, dbSchema, connectedDatabase);
        }

        /// <summary>
        /// Runs function body backfill in the background, then updates the language service
        /// and cache with enriched function schemas. Disposes the kustoClient when done.
        /// </summary>
        private async Task BackfillFunctionBodiesInBackgroundAsync(
            KustoClient kustoClient, ClusterSchema clusterSchema, string connectedDatabase, string clusterName)
        {
            try
            {
                if (clusterSchema.Databases == null || string.IsNullOrEmpty(connectedDatabase))
                    return;
                if (!clusterSchema.Databases.TryGetValue(connectedDatabase, out var dbSchema))
                    return;
                if (dbSchema.EntityGroups == null || dbSchema.EntityGroups.Count == 0)
                    return;

                await BackfillFunctionBodiesAsync(kustoClient, dbSchema, connectedDatabase);

                // Re-update language service with enriched function bodies
                _languageService.AddOrUpdateCluster(clusterName, clusterSchema);

                // Re-save cache with enriched schemas
                if (_cacheManager != null)
                {
                    await _cacheManager.SaveSchemaToCacheAsync(clusterName, clusterSchema);
                }
            }
            catch { }
            finally
            {
                kustoClient?.Dispose();
            }
        }

        /// <summary>
        /// Backfills table schemas for entity group databases.
        /// </summary>
        private static async Task BackfillTablesAsync(
            KustoClient kustoClient, DatabaseSchema dbSchema, string databaseName)
        {
            // First try .show database schema as json (fast, single call)
            try
            {
                var resolvedSchema = await kustoClient.GetDatabaseSchemaAsync(databaseName);
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

            // If we still have no tables, fall back to getschema queries for shortcut functions
            if (dbSchema.Tables == null || dbSchema.Tables.Count == 0)
            {
                await BackfillViaGetSchemaAsync(kustoClient, dbSchema, databaseName);
            }
        }

        /// <summary>
        /// Resolves function output schemas via getschema queries and replaces unparseable
        /// macro-expand bodies with datatable(...)[]-style bodies the language service can parse.
        /// </summary>
        private static async Task BackfillFunctionBodiesAsync(
            KustoClient kustoClient, DatabaseSchema dbSchema, string databaseName)
        {
            if (dbSchema.Functions == null || dbSchema.Functions.Count == 0)
                return;

            // Collect non-shortcut functions that have macro-expand bodies
            var functionsToResolve = new List<(string Name, IList<FunctionParameterSchema>? Parameters)>();
            foreach (var funcKvp in dbSchema.Functions)
            {
                var fs = funcKvp.Value;
                if (fs.Body != null && fs.Body.Contains("macro-expand"))
                {
                    // Skip shortcuts — they're already promoted to TableSymbols
                    if (fs.Folder == "Shortcuts"
                        && (fs.InputParameters == null || fs.InputParameters.Count == 0))
                        continue;

                    functionsToResolve.Add((funcKvp.Key, fs.InputParameters));
                }
            }

            if (functionsToResolve.Count == 0)
                return;

            try
            {
                var schemas = await kustoClient.GetFunctionSchemasViaQueryAsync(
                    databaseName, functionsToResolve);

                foreach (var kvp in schemas)
                {
                    if (!dbSchema.Functions.TryGetValue(kvp.Key, out var funcSchema))
                        continue;

                    // Build a datatable body from the resolved columns
                    var datatableBody = BuildDatatableBody(kvp.Value);
                    funcSchema.Body = datatableBody;
                }
            }
            catch { }
        }

        /// <summary>
        /// Builds a datatable(col1:type1, col2:type2, ...)[] function body string
        /// from a list of column definitions.
        /// </summary>
        private static string BuildDatatableBody(List<(string Name, string DataType, string CslType)> columns)
        {
            var colDefs = columns.Select(c => $"{c.Name}:{c.CslType}");
            return $"{{ datatable({string.Join(", ", colDefs)})[] }}";
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
        /// Checks whether functions in the connected database still have macro-expand bodies
        /// that need to be replaced with parseable datatable bodies.
        /// </summary>
        private static bool NeedsFunctionBodyBackfill(ClusterSchema clusterSchema, string connectedDatabase)
        {
            if (clusterSchema.Databases == null || string.IsNullOrEmpty(connectedDatabase))
                return false;
            if (!clusterSchema.Databases.TryGetValue(connectedDatabase, out var dbSchema))
                return false;
            if (dbSchema.EntityGroups == null || dbSchema.EntityGroups.Count == 0)
                return false;
            if (dbSchema.Functions == null)
                return false;

            // Check if any non-shortcut function still has a macro-expand body
            foreach (var funcKvp in dbSchema.Functions)
            {
                var fs = funcKvp.Value;
                if (fs.Body != null && fs.Body.Contains("macro-expand"))
                {
                    if (fs.Folder == "Shortcuts"
                        && (fs.InputParameters == null || fs.InputParameters.Count == 0))
                        continue;
                    return true;
                }
            }
            return false;
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
