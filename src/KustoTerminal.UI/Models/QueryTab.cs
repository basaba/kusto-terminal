using System.Drawing;
using KustoTerminal.Core.Models;

namespace KustoTerminal.UI.Models;

public class QueryTab
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "Query 1";
    public string QueryText { get; set; } = string.Empty;
    public QueryResult? LastResult { get; set; }
    public string? QueryTextForResult { get; set; }
    public KustoConnection? ConnectionForResult { get; set; }
    public Point CursorPosition { get; set; } = Point.Empty;
}