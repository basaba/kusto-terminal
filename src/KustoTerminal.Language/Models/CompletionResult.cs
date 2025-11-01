using System;
using System.Collections.Generic;

namespace KustoTerminal.Language.Models
{
    public class CompletionResult
    {
        public IReadOnlyList<CompletionItem> Items { get; set; } = new List<CompletionItem>();
    }

    public class CompletionItem
    {
        public string DisplayText { get; set; } = null!;
        public string ApplyText { get; set; } = null!;
        public string OrderText { get; set; } = null!;
    }
}