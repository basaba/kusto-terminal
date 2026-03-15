using System;

namespace KustoTerminal.Core.Models;

public class TabState
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "New Tab";
    public string QueryText { get; set; } = string.Empty;
    public string? ConnectionId { get; set; }
    public string? ClusterUri { get; set; }
    public string? Database { get; set; }
    public int Order { get; set; }
    public bool IsActive { get; set; }
}
