using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;

namespace KustoTerminal.Core.Services;

public class AzureCliAuthenticationProvider : IAuthenticationProvider
{
    private readonly AzureCliCredential _credential;
    private readonly string[] _scopes = { "https://kusto.kusto.windows.net/.default" };

    public AuthenticationType AuthType => AuthenticationType.AzureCli;

    public AzureCliAuthenticationProvider()
    {
        _credential = new AzureCliCredential();
    }

    public async Task<string> GetAccessTokenAsync(string clusterUri)
    {
        try
        {
            var tokenRequestContext = new TokenRequestContext(_scopes);
            var token = await _credential.GetTokenAsync(tokenRequestContext);
            return token.Token;
        }
        catch (CredentialUnavailableException ex)
        {
            throw new InvalidOperationException(
                "Azure CLI authentication is not available. Please run 'az login' first.", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to get access token from Azure CLI: {ex.Message}", ex);
        }
    }

    public bool IsAuthenticated()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "az",
                    Arguments = "account show",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ValidateAuthenticationAsync()
    {
        try
        {
            await GetAccessTokenAsync("https://help.kusto.windows.net");
            return true;
        }
        catch
        {
            return false;
        }
    }
}