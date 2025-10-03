using System;
using System.Collections.Generic;

namespace KustoTerminal.Language.Models
{
    public class CompletionResult
    {
        public IReadOnlyList<string> Items { get; set; } = new List<string>();
    }
}