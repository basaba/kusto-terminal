using System;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;

namespace KustoTerminal.Core.Services;

public static class AuthenticationProviderFactory
{
    public static IAuthenticationProvider? CreateProvider(AuthenticationType authType)
    {
        return authType switch
        {
            AuthenticationType.None => new NoAuthenticationProvider(),
            AuthenticationType.AzureCli => new AzureCliAuthenticationProvider(), // Will be handled at higher level
            _ => throw new ArgumentException($"Unknown authentication type: {authType}", nameof(authType))
        };
    }
}