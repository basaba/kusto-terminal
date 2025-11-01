using System;
using System.Collections.Generic;
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
        /// Fetches cluster schema and updates the language service for the given connection
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
                    // Update language service with cached schema
                    _languageService.AddOrUpdateCluster(clusterName, clusterSchema);
                    return;
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
