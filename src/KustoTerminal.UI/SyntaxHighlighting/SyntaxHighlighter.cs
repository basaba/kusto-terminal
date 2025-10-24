using System;
using System.Collections.Generic;
using System.Linq;
using Kusto.Language.Editor;
using KustoTerminal.Language.Models;
using KustoTerminal.Language.Services;
using Terminal.Gui.Views;
using Terminal.Gui.Drawing;

using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;
using KustoTerminal.Core;
using KustoTerminal.Core.Models;

namespace KustoTerminal.UI.SyntaxHighlighting
{
    public class SyntaxHighlighter
    {
        private readonly LanguageService _languageService;

        public SyntaxHighlighter(LanguageService languageService)
        {
            _languageService = languageService;
        }

        public void Highlight(TextView textView, KustoConnection connection)
        {
            var textModel = new TextModel(textView);
            var clusterName = connection?.GetClusterNameFromUrl() ?? "";
            var classificationResult = _languageService.GetClassifications(textModel, clusterName, connection?.Database ?? "");

            ApplyClassifications(textView, classificationResult.Classifications);
        }

        private void ApplyClassifications(TextView textView, Classification[] classifications)
        {
            // Default attribute for unclassified text
            var defaultAttribute = new Attribute(Color.White, Color.Black);
            
            var pos = 0;
            
            for (var y = 0; y < textView.Lines; y++)
            {
                List<Cell> line = textView.GetLine(y);
                
                for (var x = 0; x < line.Count; x++)
                {
                    Cell cell = line[x];
                    
                    // Find the classification that contains this position
                    var classification = classifications.FirstOrDefault(c => 
                        pos >= c.Start && pos < c.Start + c.Length);
                    
                    if (classification != null)
                    {
                        var attribute = ClassificationColorMapper.GetAttributeForClassification(classification.Kind);
                        cell.Attribute = attribute ?? defaultAttribute;
                    }
                    else
                    {
                        cell.Attribute = defaultAttribute;
                    }
                    
                    line[x] = cell;
                    pos++;
                }
                
                // Account for the newline character(s) that exist in Text but not in the returned lines
                pos += Environment.NewLine.Length;
            }
        }
    }
}
