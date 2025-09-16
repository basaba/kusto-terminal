using System;
using System.Threading.Tasks;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;

namespace KustoTerminal.Core.Services
{
    public class NoAuthenticationProvider : IAuthenticationProvider
    {
        public AuthenticationType AuthType => AuthenticationType.None;

        public Task<string> GetAccessTokenAsync(string clusterUri)
        {
            // Return empty string for unauthenticated connections
            return Task.FromResult(string.Empty);
        }

        public bool IsAuthenticated()
        {
            // For unauthenticated connections, we're always "authenticated" (no auth required)
            return true;
        }

        public Task<bool> ValidateAuthenticationAsync()
        {
            // For unauthenticated connections, validation always succeeds
            return Task.FromResult(true);
        }
    }
}