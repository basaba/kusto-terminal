using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace KustoTerminal.UI.Controls;

/// <summary>
/// TextView subclass that guards against ArgumentOutOfRangeException in
/// Terminal.Gui's ProcessDoubleClickSelection (bug in v2.0.0-alpha.3721
/// when double-clicking at the end of a line).
/// </summary>
public class SafeTextView : TextView
{
    protected override bool OnMouseEvent(MouseEventArgs mouseEvent)
    {
        try
        {
            return base.OnMouseEvent(mouseEvent);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Terminal.Gui bug: ProcessDoubleClickSelection passes negative
            // count to List.GetRange() at end-of-line. Swallow and continue.
            return true;
        }
    }
}
