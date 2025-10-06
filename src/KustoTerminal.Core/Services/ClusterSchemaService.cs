using System;
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
        public ClusterSchemaService(LanguageService languageService)
        {
            _languageService = languageService;
        }

        /// <summary>
        /// Fetches cluster schema and updates the language service for the given connection
        /// </summary>
        /// <param name="connection">The Kusto connection to fetch schema for</param>
        /// <returns>Task representing the async operation</returns>
        public async Task FetchAndUpdateClusterSchemaAsync(KustoConnection connection)
        {
            if (connection == null || !connection.IsValid())
            {
                return;
            }

            // Create authentication provider and Kusto client
            var authProvider = AuthenticationProviderFactory.CreateProvider(connection.AuthType);
            var kustoClient = new KustoClient(connection, authProvider);

            try
            {
                var clusterSchema = await kustoClient.GetClusterSchemaAsync();
                if (clusterSchema != null)
                {
                    // Extract cluster name from URI or use connection name
                    var clusterName = connection.GetClusterNameFromUrl();

                    // Update language service with the schema
                    _languageService.AddOrUpdateCluster(clusterName, clusterSchema);
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
        /// <returns>Task representing the async operation</returns>
        public async Task FetchAndUpdateMultipleClusterSchemasAsync(IEnumerable<KustoConnection> connections)
        {
            var tasks = new List<Task>();
            foreach (var connection in connections)
            {
                // Launch each schema fetch as a separate task
                tasks.Add(FetchAndUpdateClusterSchemaAsync(connection));
            }
            
            await Task.WhenAll(tasks);
        }
    }
}
