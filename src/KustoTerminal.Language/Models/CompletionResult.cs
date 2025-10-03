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
        public string DisplayText { get; set; }
        public string ApplyText { get; set; }
        public string OrderText { get; set; }
    }
}