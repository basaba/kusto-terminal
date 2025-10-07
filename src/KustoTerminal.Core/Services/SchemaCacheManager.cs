using System.Security.Cryptography;
using System.Text;
using Kusto.Data.Common;
using KustoTerminal.Core.Models;
using Newtonsoft.Json;

namespace KustoTerminal.Core.Services
{
    /// <summary>
    /// Manages disk-based caching of cluster schemas
    /// </summary>
    public class SchemaCacheManager
    {
        private readonly CacheConfiguration _configuration;

        public SchemaCacheManager(CacheConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            if (!_configuration.IsValid())
            {
                throw new ArgumentException("Invalid cache configuration", nameof(configuration));
            }

            // Create cache directory if it doesn't exist and caching is enabled
            if (_configuration.EnableDiskCache)
            {
                EnsureCacheDirectoryExists();
            }
        }

        /// <summary>
        /// Gets the cached schema for a cluster, if available and not expired
        /// </summary>
        /// <param name="clusterName">The cluster name</param>
        /// <returns>The cached schema, or null if not found or expired</returns>
        public async Task<ClusterSchema?> GetCachedSchemaAsync(string clusterName)
        {
            if (!_configuration.EnableDiskCache || string.IsNullOrWhiteSpace(clusterName))
            {
                return null;
            }

            try
            {
                var cacheFilePath = GetCacheFilePath(clusterName);
                
                if (!File.Exists(cacheFilePath))
                {
                    return null;
                }

                // Check if cache is expired
                if (IsCacheExpired(cacheFilePath))
                {
                    // Delete expired cache file
                    File.Delete(cacheFilePath);
                    return null;
                }

                // Read and deserialize the cached schema
                var jsonContent = await File.ReadAllTextAsync(cacheFilePath);
                var cachedData = JsonConvert.DeserializeObject<CachedSchemaData>(jsonContent);

                if (cachedData?.Schema == null)
                {
                    return null;
                }

                return cachedData.Schema;
            }
            catch (Exception)
            {
                // If there's any error reading the cache, return null
                return null;
            }
        }

        /// <summary>
        /// Saves a cluster schema to disk cache
        /// </summary>
        /// <param name="clusterName">The cluster name</param>
        /// <param name="schema">The schema to cache</param>
        public async Task SaveSchemaToCacheAsync(string clusterName, ClusterSchema schema)
        {
            if (!_configuration.EnableDiskCache || string.IsNullOrWhiteSpace(clusterName) || schema == null)
            {
                return;
            }

            try
            {
                EnsureCacheDirectoryExists();

                var cacheFilePath = GetCacheFilePath(clusterName);
                
                var cachedData = new CachedSchemaData
                {
                    ClusterName = clusterName,
                    CachedAt = DateTime.UtcNow,
                    Schema = schema
                };

                var jsonContent = JsonConvert.SerializeObject(cachedData, Formatting.Indented);
                await File.WriteAllTextAsync(cacheFilePath, jsonContent);
            }
            catch (Exception)
            {
                // Silently fail if we can't write to cache
                // This shouldn't prevent the application from functioning
            }
        }

        /// <summary>
        /// Clears the cache for a specific cluster
        /// </summary>
        /// <param name="clusterName">The cluster name</param>
        public void ClearClusterCache(string clusterName)
        {
            if (!_configuration.EnableDiskCache || string.IsNullOrWhiteSpace(clusterName))
            {
                return;
            }

            try
            {
                var cacheFilePath = GetCacheFilePath(clusterName);
                if (File.Exists(cacheFilePath))
                {
                    File.Delete(cacheFilePath);
                }
            }
            catch (Exception)
            {
                // Silently fail
            }
        }

        /// <summary>
        /// Clears all cached schemas
        /// </summary>
        public void ClearAllCache()
        {
            if (!_configuration.EnableDiskCache)
            {
                return;
            }

            try
            {
                if (Directory.Exists(_configuration.CacheDirectory))
                {
                    var cacheFiles = Directory.GetFiles(_configuration.CacheDirectory, "*.json");
                    foreach (var file in cacheFiles)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail
            }
        }

        /// <summary>
        /// Gets all cached cluster names
        /// </summary>
        /// <returns>List of cached cluster names</returns>
        public List<string> GetCachedClusterNames()
        {
            if (!_configuration.EnableDiskCache)
            {
                return new List<string>();
            }

            try
            {
                if (!Directory.Exists(_configuration.CacheDirectory))
                {
                    return new List<string>();
                }

                var cacheFiles = Directory.GetFiles(_configuration.CacheDirectory, "*.json");
                var clusterNames = new List<string>();

                foreach (var file in cacheFiles)
                {
                    try
                    {
                        var jsonContent = File.ReadAllText(file);
                        var cachedData = JsonConvert.DeserializeObject<CachedSchemaData>(jsonContent);
                        if (cachedData?.ClusterName != null)
                        {
                            clusterNames.Add(cachedData.ClusterName);
                        }
                    }
                    catch
                    {
                        // Skip invalid files
                    }
                }

                return clusterNames;
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Cleans up expired cache files
        /// </summary>
        public void CleanupExpiredCache()
        {
            if (!_configuration.EnableDiskCache || _configuration.CacheExpirationHours == 0)
            {
                return;
            }

            try
            {
                if (!Directory.Exists(_configuration.CacheDirectory))
                {
                    return;
                }

                var cacheFiles = Directory.GetFiles(_configuration.CacheDirectory, "*.json");
                foreach (var file in cacheFiles)
                {
                    if (IsCacheExpired(file))
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail
            }
        }

        /// <summary>
        /// Ensures the cache directory exists
        /// </summary>
        private void EnsureCacheDirectoryExists()
        {
            if (!Directory.Exists(_configuration.CacheDirectory))
            {
                Directory.CreateDirectory(_configuration.CacheDirectory);
            }
        }

        /// <summary>
        /// Gets the cache file path for a cluster
        /// </summary>
        /// <param name="clusterName">The cluster name</param>
        /// <returns>The full path to the cache file</returns>
        private string GetCacheFilePath(string clusterName)
        {
            // Create a safe filename from the cluster name using hash
            var safeFileName = GetSafeFileName(clusterName);
            return Path.Combine(_configuration.CacheDirectory, $"{safeFileName}.json");
        }

        /// <summary>
        /// Creates a safe filename from a cluster name
        /// </summary>
        /// <param name="clusterName">The cluster name</param>
        /// <returns>A safe filename</returns>
        private string GetSafeFileName(string clusterName)
        {
            // Use SHA256 hash to create a consistent, safe filename
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(clusterName));
                var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                // Take first 16 characters of hash and append cluster name prefix
                var prefix = new string(clusterName.Where(char.IsLetterOrDigit).Take(20).ToArray());
                return $"{prefix}_{hashString.Substring(0, 16)}";
            }
        }

        /// <summary>
        /// Checks if a cache file is expired
        /// </summary>
        /// <param name="cacheFilePath">The cache file path</param>
        /// <returns>True if expired, false otherwise</returns>
        private bool IsCacheExpired(string cacheFilePath)
        {
            if (_configuration.CacheExpirationHours == 0)
            {
                return false; // No expiration
            }

            try
            {
                var fileInfo = new FileInfo(cacheFilePath);
                var expirationTime = fileInfo.LastWriteTimeUtc.AddHours(_configuration.CacheExpirationHours);
                return DateTime.UtcNow > expirationTime;
            }
            catch
            {
                return true; // If we can't read the file, consider it expired
            }
        }

        /// <summary>
        /// Internal class for storing cached schema data
        /// </summary>
        private class CachedSchemaData
        {
            public string ClusterName { get; set; } = string.Empty;
            public DateTime CachedAt { get; set; }
            public ClusterSchema? Schema { get; set; }
        }
    }
}
