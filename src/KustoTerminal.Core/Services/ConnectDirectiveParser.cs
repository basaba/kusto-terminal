using System;
using System.Text.RegularExpressions;

namespace KustoTerminal.Core.Services;

/// <summary>
/// Parses #connect directives from KQL script blocks.
/// Supported syntax:
///   #connect cluster('name').database('db')
///   #connect cluster('name')
/// Cluster name shorthand:
///   No dots        → name.kusto.windows.net
///   One dot        → name.region.kusto.windows.net  (treated as name.region)
///   Multiple dots  → FQDN as-is
/// </summary>
public static class ConnectDirectiveParser
{
    // Matches: #connect cluster('...') with optional .database('...')
    // Groups: 1=cluster value, 3=database value (optional)
    private static readonly Regex s_pattern = new(
        @"^\s*#connect\s+cluster\s*\(\s*['""]([^'""]+)['""]\s*\)(?:\s*\.\s*database\s*\(\s*['""]([^'""]+)['""]\s*\))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Tries to parse a #connect directive from the start of a query block.
    /// </summary>
    /// <param name="query">The full query block text</param>
    /// <param name="clusterUri">The resolved cluster URI (always https://)</param>
    /// <param name="clusterDisplayName">The original cluster value as specified by the user</param>
    /// <param name="database">The database name, or null if not specified</param>
    /// <param name="remainingQuery">The query text after the #connect line</param>
    /// <returns>True if a valid #connect directive was found</returns>
    public static bool TryParse(string query, out string clusterUri, out string clusterDisplayName, out string? database, out string remainingQuery)
    {
        clusterUri = "";
        clusterDisplayName = "";
        database = null;
        remainingQuery = "";

        if (string.IsNullOrWhiteSpace(query))
            return false;

        // Split into first line and rest
        var newlineIndex = query.IndexOf('\n');
        var firstLine = newlineIndex >= 0 ? query[..newlineIndex] : query;
        remainingQuery = newlineIndex >= 0 ? query[(newlineIndex + 1)..].Trim() : "";

        var match = s_pattern.Match(firstLine);
        if (!match.Success)
            return false;

        var clusterValue = match.Groups[1].Value.Trim();
        database = match.Groups[2].Success ? match.Groups[2].Value.Trim() : null;

        clusterDisplayName = clusterValue;
        clusterUri = ResolveClusterUri(clusterValue);
        return true;
    }

    /// <summary>
    /// Resolves a cluster name/shorthand to a full https:// URI.
    /// </summary>
    internal static string ResolveClusterUri(string clusterValue)
    {
        // Already a full URI
        if (clusterValue.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || clusterValue.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return clusterValue;
        }

        var dotCount = clusterValue.Split('.').Length - 1;

        if (dotCount == 0)
        {
            // Simple name: "help" → "https://help.kusto.windows.net"
            return $"https://{clusterValue}.kusto.windows.net";
        }
        else if (dotCount == 1)
        {
            // Name.region: "help.westeurope" → "https://help.westeurope.kusto.windows.net"
            return $"https://{clusterValue}.kusto.windows.net";
        }
        else
        {
            // FQDN: "help.kusto.windows.net" → "https://help.kusto.windows.net"
            return $"https://{clusterValue}";
        }
    }
}
