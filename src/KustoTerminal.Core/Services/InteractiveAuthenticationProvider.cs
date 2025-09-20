using System;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;

namespace KustoTerminal.Core.Services
{
    public class InteractiveAuthenticationProvider : IAuthenticationProvider
    {
        private readonly InteractiveBrowserCredential _credential;
        private readonly string[] _scopes = { "https://kusto.kusto.windows.net/.default" };
        private AccessToken? _cachedToken;

        public AuthenticationType AuthType => AuthenticationType.Interactive;

        public InteractiveAuthenticationProvider()
        {
            var options = new InteractiveBrowserCredentialOptions
            {
                // Use default tenant (common) for multi-tenant scenarios
                TenantId = null,
                // Redirect to localhost for better user experience
                RedirectUri = new Uri("http://localhost:8400"),
                // Use system browser for better compatibility
                ClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46" // Azure CLI client ID for compatibility
            };

            _credential = new InteractiveBrowserCredential(options);
        }

        public async Task<string> GetAccessTokenAsync(string clusterUri)
        {
            try
            {
                // Check if we have a valid cached token
                if (_cachedToken.HasValue && _cachedToken.Value.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
                {
                    return _cachedToken.Value.Token;
                }

                // Request a new token
                var tokenRequestContext = new TokenRequestContext(_scopes);
                var token = await _credential.GetTokenAsync(tokenRequestContext);
                
                // Cache the token
                _cachedToken = token;
                
                return token.Token;
            }
            catch (AuthenticationFailedException ex)
            {
                throw new InvalidOperationException(
                    "Interactive authentication failed. Please ensure you have proper access to the Azure resources.", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to get access token through interactive authentication: {ex.Message}", ex);
            }
        }

        public bool IsAuthenticated()
        {
            // For interactive authentication, we consider it authenticated if we have a valid cached token
            // or if the credential is available (it will prompt when needed)
            return _cachedToken.HasValue && _cachedToken.Value.ExpiresOn > DateTimeOffset.UtcNow ||
                   _credential != null;
        }

        public async Task<bool> ValidateAuthenticationAsync()
        {
            try
            {
                // Try to get a token to validate authentication
                await GetAccessTokenAsync("https://help.kusto.windows.net");
                return true;
            }
            catch
            {
                // Clear cached token on validation failure
                _cachedToken = null;
                return false;
            }
        }
    }
}