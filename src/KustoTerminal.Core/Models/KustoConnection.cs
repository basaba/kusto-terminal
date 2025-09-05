using System;

namespace KustoTerminal.Core.Models
{
    public class KustoConnection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string ClusterUri { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public AuthenticationType AuthType { get; set; } = AuthenticationType.AzureCli;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;
        public bool IsDefault { get; set; } = false;

        public string DisplayName => !string.IsNullOrEmpty(Name) ? Name : ClusterUri;
        
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(ClusterUri) && 
                   !string.IsNullOrWhiteSpace(Database) &&
                   Uri.TryCreate(ClusterUri, UriKind.Absolute, out _);
        }
    }

    public enum AuthenticationType
    {
        AzureCli,
        ServicePrincipal,
        Interactive
    }
}