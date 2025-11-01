using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace KustoTerminal.UI.Common;
using Terminal.Gui;

public static class ExtendedLabel
{
    public static Label AppendLabel(this Label labelToAppendTo, string text, string schemeName, List<Label>? labels = null)
    {
        var label = new Label
        {
            Text = text,
            SchemeName = schemeName,
            X = Pos.Right(labelToAppendTo),
            Y = labelToAppendTo.Y,
            Height = labelToAppendTo.Height,
            Width = Dim.Auto(DimAutoStyle.Text)
        };
        
        labels?.Add(label);
        return label;
    }
}