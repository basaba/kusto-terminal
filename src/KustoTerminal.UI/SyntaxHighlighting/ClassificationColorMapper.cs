using System.Collections.Generic;
using Kusto.Language.Editor;
using Terminal.Gui.Drawing;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace KustoTerminal.UI.SyntaxHighlighting
{
    /// <summary>
    /// Centralized mapping of ClassificationKind to colors for syntax highlighting
    /// </summary>
    public static class ClassificationColorMapper
    {
        private static readonly Dictionary<ClassificationKind, Color> _colorMap = new Dictionary<ClassificationKind, Color>
        {
            { ClassificationKind.Keyword, ColorsCollection.SkyBlue },
            { ClassificationKind.Identifier, ColorsCollection.White },
            { ClassificationKind.Literal, ColorsCollection.White },
            { ClassificationKind.StringLiteral, ColorsCollection.PaleChestnut },
            { ClassificationKind.Comment, ColorsCollection.OliveDrab },
            { ClassificationKind.Punctuation, ColorsCollection.White },
            { ClassificationKind.QueryOperator, ColorsCollection.MediumTurquoise },
            { ClassificationKind.ScalarOperator, ColorsCollection.SkyBlue },
            { ClassificationKind.MathOperator, Color.White },
            { ClassificationKind.Function, ColorsCollection.SkyBlue },
            { ClassificationKind.Type, ColorsCollection.SkyBlue },
            { ClassificationKind.Column, ColorsCollection.PaleVioletRed },
            { ClassificationKind.Table, ColorsCollection.SoftGold },
            { ClassificationKind.Database, ColorsCollection.SoftGold },
            { ClassificationKind.Parameter, ColorsCollection.LightSkyBlue },
            { ClassificationKind.Variable, ColorsCollection.LightSkyBlue },
            { ClassificationKind.MaterializedView, ColorsCollection.SoftGold },
        };

        /// <summary>
        /// Gets the Terminal.Gui Color for a given classification kind
        /// </summary>
        /// <param name="kind">The classification kind</param>
        /// <returns>The corresponding color, or null if no mapping exists</returns>
        public static Color? GetColorForClassification(ClassificationKind kind)
        {
            return _colorMap.TryGetValue(kind, out var color) ? color : null;
        }

        /// <summary>
        /// Gets the Terminal.Gui Attribute for a given classification kind
        /// </summary>
        /// <param name="kind">The classification kind</param>
        /// <param name="backgroundColor">The background color to use (defaults to black)</param>
        /// <returns>The corresponding attribute, or null if no mapping exists</returns>
        public static Attribute? GetAttributeForClassification(ClassificationKind kind, Color? backgroundColor = null)
        {
            var bgColor = backgroundColor ?? Color.Black;
            var color = GetColorForClassification(kind);
            return color.HasValue ? new Attribute(color.Value, bgColor) : null;
        }

        /// <summary>
        /// Gets the HTML hex color string for a given classification kind
        /// </summary>
        /// <param name="kind">The classification kind</param>
        /// <returns>The corresponding HTML hex color string, or null if no mapping exists</returns>
        public static string GetHtmlColorForClassification(ClassificationKind kind)
        {
            var color = GetColorForClassification(kind);
            return color.HasValue ? ColorToHex(color.Value) : null!;
        }

        /// <summary>
        /// Converts Terminal.Gui Color to HTML hex color code
        /// </summary>
        /// <param name="color">The Terminal.Gui color</param>
        /// <returns>HTML hex color string</returns>
        private static string ColorToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }
}
