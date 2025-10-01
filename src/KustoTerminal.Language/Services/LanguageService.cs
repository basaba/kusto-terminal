using System;
using Kusto.Language;
using Kusto.Language.Editor;
using Kusto.Language.Utils;
using KustoTerminal.Language.Models;

namespace KustoTerminal.Language.Services
{
    public class LanguageService
    {
        public ClassificationResult GetClassifications(ITextModel model)
        {
            var classifications = CodeScript.From(model.GetText())
            .Blocks
            .SelectMany(block => block.Service.GetClassifications(block.Start, block.Length).Classifications)
            .Select(classification => new Classification
            {
                Kind = classification.Kind,
                Start = classification.Start,
                Length = classification.Length
            })
            .ToList();

            return new ClassificationResult { Classifications = classifications.ToArray() };
        }
    }
}
