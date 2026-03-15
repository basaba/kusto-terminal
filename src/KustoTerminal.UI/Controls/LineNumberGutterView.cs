using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace KustoTerminal.UI.Controls;

/// <summary>
/// Gutter view that displays line numbers synchronized with a TextView's scroll position.
/// Placed to the left of the TextView in the query editor.
/// </summary>
public class LineNumberGutterView : View
{
    private TextView? _textView;
    private int _lastTopRow = -1;
    private int _lastLineCount = -1;
    private int _lastCurrentRow = -1;
    private int _gutterWidth = 4; // minimum: "  1 " = 4 chars

    private static readonly Attribute s_normalAttr = new(new Color(100, 100, 100), Color.Black);
    private static readonly Attribute s_currentAttr = new(new Color(220, 220, 180), Color.Black);
    private static readonly Attribute s_separatorAttr = new(new Color(60, 60, 60), Color.Black);

    public LineNumberGutterView()
    {
        CanFocus = false;
    }

    /// <summary>Bind to a TextView for scroll and line count synchronization.</summary>
    public void SetTextView(TextView textView)
    {
        _textView = textView;
    }

    /// <summary>
    /// Recalculate gutter width based on the total line count.
    /// Returns true if width changed (caller should update layout).
    /// </summary>
    public bool UpdateWidth()
    {
        if (_textView == null) return false;

        int lineCount = Math.Max(_textView.Lines, 1);
        int digits = lineCount < 10 ? 1
            : lineCount < 100 ? 2
            : lineCount < 1000 ? 3
            : lineCount < 10000 ? 4
            : 5;

        // Format: " {number} │" — digits + 1 leading space + 2 trailing (space + separator)
        int newWidth = digits + 3;
        if (newWidth < 4) newWidth = 4;

        if (newWidth != _gutterWidth)
        {
            _gutterWidth = newWidth;
            Width = _gutterWidth;
            return true;
        }
        return false;
    }

    /// <summary>Current gutter width in columns.</summary>
    public int GutterWidth => _gutterWidth;

    /// <summary>Check if the gutter needs a redraw (scroll or cursor moved).</summary>
    public bool NeedsRedraw()
    {
        if (_textView == null) return false;
        return _textView.TopRow != _lastTopRow
            || _textView.Lines != _lastLineCount
            || _textView.CurrentRow != _lastCurrentRow;
    }

    protected override bool OnDrawingContent()
    {
        if (_textView == null) return true;

        var driver = Application.Driver;
        if (driver == null) return true;

        var viewport = Viewport;
        int topRow = _textView.TopRow;
        int totalLines = _textView.Lines;
        int currentRow = _textView.CurrentRow;
        int visibleRows = viewport.Height;
        int numberWidth = _gutterWidth - 3; // minus leading space, trailing space, separator

        _lastTopRow = topRow;
        _lastLineCount = totalLines;
        _lastCurrentRow = currentRow;

        for (int screenRow = 0; screenRow < visibleRows; screenRow++)
        {
            int lineNum = topRow + screenRow + 1; // 1-based
            Move(0, screenRow);

            if (lineNum <= totalLines)
            {
                bool isCurrent = (topRow + screenRow) == currentRow;
                var attr = isCurrent ? s_currentAttr : s_normalAttr;
                driver.SetAttribute(attr);

                // Right-align the number: " {number} "
                string numStr = lineNum.ToString();
                int padding = numberWidth - numStr.Length;

                // Leading space
                driver.AddRune(' ');
                // Padding
                for (int p = 0; p < padding; p++)
                    driver.AddRune(' ');
                // Number
                foreach (char c in numStr)
                    driver.AddRune(c);
                // Trailing space
                driver.AddRune(' ');

                // Separator
                driver.SetAttribute(s_separatorAttr);
                driver.AddRune('│');
            }
            else
            {
                // Past end of file — draw empty gutter
                driver.SetAttribute(s_separatorAttr);
                for (int i = 0; i < _gutterWidth - 1; i++)
                    driver.AddRune(' ');
                driver.AddRune('│');
            }
        }

        return true;
    }
}
