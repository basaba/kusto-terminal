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

namespace KustoTerminal.UI.SyntaxHighlighting
{
    public class SyntaxHighlighter
    {
        private readonly Dictionary<ClassificationKind, Attribute> _colorMap;
        
        public SyntaxHighlighter()
        {
            // Initialize color mappings for different classification types
            _colorMap = new Dictionary<ClassificationKind, Attribute>
            {
                { ClassificationKind.Keyword, new Attribute(ColorsCollection.SkyBlue, Color.Black) },
                { ClassificationKind.Identifier, new Attribute(ColorsCollection.White, Color.Black) },
                { ClassificationKind.Literal, new Attribute(ColorsCollection.White, Color.Black) },
                { ClassificationKind.StringLiteral, new Attribute(ColorsCollection.PaleChestnut, Color.Black) },
                { ClassificationKind.Comment, new Attribute(ColorsCollection.OliveDrab, Color.Black) },
                { ClassificationKind.Punctuation, new Attribute(ColorsCollection.White, Color.Black) },
                { ClassificationKind.QueryOperator, new Attribute(ColorsCollection.MediumTurquoise, Color.Black) },
                { ClassificationKind.ScalarOperator, new Attribute(ColorsCollection.SkyBlue, Color.Black) },
                { ClassificationKind.MathOperator, new Attribute(Color.White, Color.Black) },
                { ClassificationKind.Function, new Attribute(ColorsCollection.SkyBlue, Color.Black) },
                { ClassificationKind.Type, new Attribute(ColorsCollection.SkyBlue, Color.Black) },
                { ClassificationKind.Column, new Attribute(ColorsCollection.PaleVioletRed, Color.Black) },
                { ClassificationKind.Table, new Attribute(ColorsCollection.SoftGold, Color.Black) },
                { ClassificationKind.Database, new Attribute(ColorsCollection.SoftGold, Color.Black) },
                { ClassificationKind.Parameter, new Attribute(ColorsCollection.LightSkyBlue, Color.Black) },
                { ClassificationKind.Variable, new Attribute(ColorsCollection.LightSkyBlue, Color.Black) }
            };
        }

        public void Highlight(TextView textView)
        {
            var textModel = new TextModel(textView);
            var languageService = new LanguageService();
            var classificationResult = languageService.GetClassifications(textModel);
            
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
                    
                    if (classification != null && _colorMap.TryGetValue(classification.Kind, out var attribute))
                    {
                        cell.Attribute = attribute;
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
