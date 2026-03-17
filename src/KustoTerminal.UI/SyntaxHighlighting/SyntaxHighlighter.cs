using System;
using System.Collections.Generic;
using System.Linq;
using Kusto.Language.Editor;
using KustoTerminal.Core;
using KustoTerminal.Core.Models;
using KustoTerminal.Language.Models;
using KustoTerminal.Language.Services;
using Terminal.Gui.Drawing;
using Terminal.Gui.Views;

using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace KustoTerminal.UI.SyntaxHighlighting;

public class SyntaxHighlighter
{
    private readonly LanguageService _languageService;

    // Cache: skip re-parsing when text hasn't changed (e.g., during scroll)
    private string _cachedText = "";
    private string _cachedCluster = "";
    private string _cachedDatabase = "";
    private Classification[] _cachedClassifications = Array.Empty<Classification>();

    public SyntaxHighlighter(LanguageService languageService)
    {
        _languageService = languageService;
    }

    public void Highlight(TextView textView, KustoConnection connection)
    {
        var text = textView.Text ?? "";
        var clusterName = connection?.GetClusterNameFromUrl() ?? "";
        var databaseName = connection?.Database ?? "";

        // Only re-parse if the text or connection actually changed.
        // During scroll, text is unchanged — reuse cached classifications.
        if (text != _cachedText || clusterName != _cachedCluster || databaseName != _cachedDatabase)
        {
            var textModel = new TextModel(textView);
            var classificationResult = _languageService.GetClassifications(textModel, clusterName, databaseName);
            _cachedClassifications = classificationResult.Classifications;
            _cachedText = text;
            _cachedCluster = clusterName;
            _cachedDatabase = databaseName;
        }

        ApplyClassifications(textView, _cachedClassifications);
    }

    /// <summary>
    /// Invalidate the cache so the next Highlight() call re-parses.
    /// Call when external state changes (e.g., schema updates).
    /// </summary>
    public void InvalidateCache()
    {
        _cachedText = "";
    }

    private void ApplyClassifications(TextView textView, Classification[] classifications)
    {
        if (classifications.Length == 0)
        {
            ApplyDefault(textView);
        }
        else
        {
            ApplyKustoClassifications(textView, classifications);
        }

        // Apply directive highlighting on top (overrides Kusto classifications for #connect lines)
        ApplyDirectiveHighlighting(textView);
    }

    private static void ApplyDirectiveHighlighting(TextView textView)
    {
        var directiveAttribute = new Attribute(ColorsCollection.OliveDrab, Color.Black);

        for (var y = 0; y < textView.Lines; y++)
        {
            List<Cell> line = textView.GetLine(y);
            if (line.Count < 8) continue; // "#connect" is 8 chars minimum

            // Check if line starts with #connect (skip leading whitespace)
            var lineText = new string(line.Select(c => (char)c.Rune.Value).ToArray()).TrimStart();
            if (!lineText.StartsWith("#connect", StringComparison.OrdinalIgnoreCase))
                continue;

            for (var x = 0; x < line.Count; x++)
            {
                Cell cell = line[x];
                cell.Attribute = directiveAttribute;
                line[x] = cell;
            }
        }
    }

    private static void ApplyKustoClassifications(TextView textView, Classification[] classifications)
    {
        var defaultAttribute = new Attribute(Color.White, Color.Black);

        // Sort by start position for efficient linear scan (O(n+m) instead of O(n*m))
        var sorted = classifications;
        if (classifications.Length > 1)
        {
            sorted = (Classification[])classifications.Clone();
            Array.Sort(sorted, (a, b) => a.Start.CompareTo(b.Start));
        }

        var pos = 0;
        int classIdx = 0;

        for (var y = 0; y < textView.Lines; y++)
        {
            List<Cell> line = textView.GetLine(y);

            for (var x = 0; x < line.Count; x++)
            {
                // Advance past classifications that end before current position
                while (classIdx < sorted.Length
                    && sorted[classIdx].Start + sorted[classIdx].Length <= pos)
                    classIdx++;

                Cell cell = line[x];

                if (classIdx < sorted.Length
                    && pos >= sorted[classIdx].Start
                    && pos < sorted[classIdx].Start + sorted[classIdx].Length)
                {
                    cell.Attribute = ClassificationColorMapper.GetAttributeForClassification(sorted[classIdx].Kind)
                        ?? defaultAttribute;
                }
                else
                {
                    cell.Attribute = defaultAttribute;
                }

                line[x] = cell;
                pos++;
            }

            pos += Environment.NewLine.Length;
        }
    }

    private static void ApplyDefault(TextView textView)
    {
        var defaultAttribute = new Attribute(Color.White, Color.Black);
        for (var y = 0; y < textView.Lines; y++)
        {
            List<Cell> line = textView.GetLine(y);
            for (var x = 0; x < line.Count; x++)
            {
                Cell cell = line[x];
                cell.Attribute = defaultAttribute;
                line[x] = cell;
            }
        }
    }
}
