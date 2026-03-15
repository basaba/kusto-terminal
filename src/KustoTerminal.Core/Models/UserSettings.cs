using System;
using System.Collections.Generic;

namespace KustoTerminal.Core.Models;

public class UserSettings
{
    public string LastQuery { get; set; } = string.Empty;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public List<TabState> Tabs { get; set; } = new List<TabState>();
}