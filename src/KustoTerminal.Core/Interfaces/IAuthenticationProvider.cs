using KustoTerminal.Core.Models;

namespace KustoTerminal.Core.Interfaces;

public interface IAuthenticationProvider
{
    Task<string> GetAccessTokenAsync(string clusterUri);
    bool IsAuthenticated();
    Task<bool> ValidateAuthenticationAsync();
    AuthenticationType AuthType { get; }
}
