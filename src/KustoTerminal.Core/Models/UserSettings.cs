using System;

namespace KustoTerminal.Core.Models;

public class UserSettings
{
    public string LastQuery { get; set; } = string.Empty;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}