using System;
using System.Collections.Generic;
using System.ComponentModel;
using Kusto.Cloud.Platform.Utils;

namespace KustoTerminal.Core.Models
{
    public class KustoConnection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string ClusterUri { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public List<string> Databases { get; set; } = new List<string>();
        public AuthenticationType AuthType { get; set; } = AuthenticationType.AzureCli;

        public string DisplayName => !string.IsNullOrEmpty(Name) ? Name : ClusterUri;

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(ClusterUri) &&
                   !string.IsNullOrWhiteSpace(Database) &&
                   Uri.TryCreate(ClusterUri, UriKind.Absolute, out _);
        }

        public string GetClusterNameFromUrl()
        {
            // Extract cluster name from URI
            if (!Uri.TryCreate(ClusterUri, UriKind.Absolute, out var uri))
            {
                throw new Exception("Cluster URI is invalid");
            }

            var host = uri.Host;
            return host.SplitFirst(".");
        }
    }
    
    public enum AuthenticationType
    {
        None,
        AzureCli
    }
}