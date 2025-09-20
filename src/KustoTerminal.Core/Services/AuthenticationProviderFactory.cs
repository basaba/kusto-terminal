using System;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;

namespace KustoTerminal.Core.Services
{
    public static class AuthenticationProviderFactory
    {
        public static IAuthenticationProvider? CreateProvider(AuthenticationType authType)
        {
            return authType switch
            {
                AuthenticationType.None => new NoAuthenticationProvider(),
                AuthenticationType.AzureCli => new AzureCliAuthenticationProvider(),
                AuthenticationType.Interactive => new InteractiveAuthenticationProvider(),
                _ => throw new ArgumentException($"Unknown authentication type: {authType}", nameof(authType))
            };
        }
    }
}