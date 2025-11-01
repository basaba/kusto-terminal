using System;
using Kusto.Language.Editor;

namespace KustoTerminal.Language.Models;

public class Classification
{
    public ClassificationKind Kind { get; set; }
    public int Start { get; set; }
    public int Length { get; set; }
}