using System;

namespace KustoTerminal.Core.Models
{
    /// <summary>
    /// Configuration settings for cluster schema caching
    /// </summary>
    public class CacheConfiguration
    {
        /// <summary>
        /// Whether disk caching is enabled
        /// </summary>
        public bool EnableDiskCache { get; set; } = true;

        /// <summary>
        /// Directory path where cache files will be stored
        /// Default: ~/.kusto-terminal/cache
        /// </summary>
        public string CacheDirectory { get; set; } = GetDefaultCacheDirectory();

        /// <summary>
        /// Cache expiration time in hours. Set to 0 for no expiration
        /// </summary>
        public int CacheExpirationHours { get; set; } = 24;

        /// <summary>
        /// Gets the default cache directory based on the user's home directory
        /// </summary>
        private static string GetDefaultCacheDirectory()
        {
            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDirectory, ".kusto-terminal", "cache");
        }

        /// <summary>
        /// Validates the cache configuration
        /// </summary>
        public bool IsValid()
        {
            if (!EnableDiskCache)
                return true;

            if (string.IsNullOrWhiteSpace(CacheDirectory))
                return false;

            if (CacheExpirationHours < 0)
                return false;

            return true;
        }
    }
}
