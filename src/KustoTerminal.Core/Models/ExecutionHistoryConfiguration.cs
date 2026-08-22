namespace KustoTerminal.Core.Models;

public sealed class ExecutionHistoryConfiguration
{
    public string HistoryDirectory { get; set; } = GetDefaultHistoryDirectory();
    public int RetentionDays { get; set; } = 30;
    public int RetentionEntries { get; set; } = 1000;

    private static string GetDefaultHistoryDirectory()
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDirectory, ".kusto-terminal", "history");
    }
}
