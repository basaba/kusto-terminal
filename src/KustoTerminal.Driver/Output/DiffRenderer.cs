using System.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using TguiAttribute = Terminal.Gui.Drawing.Attribute;

namespace KustoTerminal.Driver.Output;

/// <summary>
/// Double-buffer diff renderer that compares the current screen state (front buffer) against
/// the desired state (back buffer from Terminal.Gui's OutputBuffer) and emits only the minimal
/// ANSI escape sequences needed to update changed cells.
/// </summary>
public sealed class DiffRenderer
{
    private Cell[,]? _frontBuffer;
    private int _lastRows;
    private int _lastCols;
    private readonly AnsiOutputBuffer _ansi = new();
    private readonly SgrCache _sgrCache = new();

    // Track current cursor position and attribute to minimize escape sequences
    private int _cursorRow = -1;
    private int _cursorCol = -1;
    private TguiAttribute? _currentAttr;

    /// <summary>
    /// Renders the diff between the front buffer (what's on screen) and the back buffer
    /// (what Terminal.Gui wants on screen). Returns the ANSI string to write to stdout.
    /// After rendering, the front buffer is updated to match the back buffer.
    /// </summary>
    public string Render(Cell[,] backBuffer, bool[] dirtyLines, int rows, int cols)
    {
        _ansi.Reset();

        EnsureFrontBuffer(rows, cols);

        _ansi.HideCursor();

        for (int row = 0; row < rows; row++)
        {
            if (!dirtyLines[row])
                continue;

            RenderRow(backBuffer, row, cols);

            dirtyLines[row] = false;
        }

        // Copy back buffer to front buffer for dirty rows
        CopyToFrontBuffer(backBuffer, rows, cols);

        return _ansi.ToString();
    }

    /// <summary>
    /// Force a full screen render (no diffing). Used on first render or after resize.
    /// </summary>
    public string RenderFull(Cell[,] backBuffer, int rows, int cols)
    {
        _ansi.Reset();

        EnsureFrontBuffer(rows, cols);

        _ansi.HideCursor();
        _ansi.ClearScreen();
        _currentAttr = null;
        _cursorRow = -1;
        _cursorCol = -1;

        for (int row = 0; row < rows; row++)
        {
            RenderRow(backBuffer, row, cols);
        }

        CopyToFrontBuffer(backBuffer, rows, cols);

        return _ansi.ToString();
    }

    /// <summary>
    /// Invalidates the front buffer, forcing a full redraw on next render.
    /// </summary>
    public void Invalidate()
    {
        _frontBuffer = null;
        _currentAttr = null;
        _cursorRow = -1;
        _cursorCol = -1;
    }

    private void RenderRow(Cell[,] backBuffer, int row, int cols)
    {
        int col = 0;

        while (col < cols)
        {
            var backCell = backBuffer[row, col];

            // Skip unchanged cells (diff against front buffer)
            if (_frontBuffer != null
                && row < _lastRows && col < _lastCols
                && CellsEqual(_frontBuffer[row, col], backCell))
            {
                col++;
                continue;
            }

            // Need to write this cell. First, position the cursor.
            if (_cursorRow != row || _cursorCol != col)
            {
                // Choose shortest cursor movement
                if (_cursorRow == row && _cursorCol >= 0)
                {
                    int delta = col - _cursorCol;
                    if (delta > 0 && delta <= 4)
                        _ansi.MoveRight(delta);
                    else if (delta < 0 && delta >= -4)
                        _ansi.MoveLeft(-delta);
                    else
                        _ansi.MoveTo(row, col);
                }
                else
                {
                    _ansi.MoveTo(row, col);
                }
                _cursorRow = row;
                _cursorCol = col;
            }

            // Set attribute if changed
            var attr = backCell.Attribute ?? TguiAttribute.Default;
            if (!AttributesEqual(_currentAttr, attr))
            {
                _ansi.AppendRaw(_sgrCache.GetSgrSequence(attr));
                _currentAttr = attr;
            }

            // Write the character
            var rune = backCell.Rune;
            if (rune.Value == 0 || rune.Value == '\0')
            {
                _ansi.AppendRune(' ');
            }
            else
            {
                _ansi.AppendRune(rune);
            }

            int runeWidth = GetRuneWidth(rune);
            _cursorCol += runeWidth;
            col += runeWidth;
        }
    }

    private void EnsureFrontBuffer(int rows, int cols)
    {
        if (_frontBuffer == null || _lastRows != rows || _lastCols != cols)
        {
            _frontBuffer = new Cell[rows, cols];
            // Initialize front buffer with spaces - forces full draw on first render
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    _frontBuffer[r, c] = new Cell
                    {
                        Rune = new Rune(' '),
                        Attribute = null,
                        IsDirty = false
                    };
                }
            }
            _lastRows = rows;
            _lastCols = cols;
            _currentAttr = null;
            _cursorRow = -1;
            _cursorCol = -1;
        }
    }

    private void CopyToFrontBuffer(Cell[,] backBuffer, int rows, int cols)
    {
        if (_frontBuffer == null) return;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                _frontBuffer[r, c] = backBuffer[r, c];
            }
        }
    }

    private static bool CellsEqual(Cell a, Cell b)
    {
        return a.Rune == b.Rune && AttributesEqual(a.Attribute, b.Attribute);
    }

    private static bool AttributesEqual(TguiAttribute? a, TguiAttribute? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.Value.Foreground == b.Value.Foreground && a.Value.Background == b.Value.Background;
    }

    private static int GetRuneWidth(Rune rune)
    {
        // CJK and other wide characters take 2 columns
        var width = Rune.GetUnicodeCategory(rune) switch
        {
            System.Globalization.UnicodeCategory.OtherNotAssigned => 1,
            _ => rune.Utf16SequenceLength <= 1 && rune.Value < 0x1100 ? 1 :
                 IsWideChar(rune.Value) ? 2 : 1
        };
        return Math.Max(1, width);
    }

    private static bool IsWideChar(int codePoint)
    {
        // Simplified wide char detection for CJK ranges
        return (codePoint >= 0x1100 && codePoint <= 0x115F) ||  // Hangul Jamo
               (codePoint >= 0x2E80 && codePoint <= 0x303E) ||  // CJK Radicals
               (codePoint >= 0x3040 && codePoint <= 0x33BF) ||  // Japanese
               (codePoint >= 0x3400 && codePoint <= 0x4DBF) ||  // CJK Unified Ext A
               (codePoint >= 0x4E00 && codePoint <= 0x9FFF) ||  // CJK Unified
               (codePoint >= 0xAC00 && codePoint <= 0xD7AF) ||  // Hangul Syllables
               (codePoint >= 0xF900 && codePoint <= 0xFAFF) ||  // CJK Compatibility
               (codePoint >= 0xFE30 && codePoint <= 0xFE6F) ||  // CJK Compatibility Forms
               (codePoint >= 0xFF01 && codePoint <= 0xFF60) ||  // Fullwidth Forms
               (codePoint >= 0xFFE0 && codePoint <= 0xFFE6) ||  // Fullwidth Signs
               (codePoint >= 0x20000 && codePoint <= 0x2FFFD) || // CJK Ext B-F
               (codePoint >= 0x30000 && codePoint <= 0x3FFFD);   // CJK Ext G
    }
}

/// <summary>
/// Caches SGR (Select Graphic Rendition) escape sequences for color attributes
/// to avoid regenerating identical strings.
/// </summary>
internal sealed class SgrCache
{
    private readonly Dictionary<(int fg, int bg), string> _cache = new(256);

    public string GetSgrSequence(TguiAttribute attr)
    {
        var fg = attr.Foreground;
        var bg = attr.Background;
        var key = (ColorToArgb(fg), ColorToArgb(bg));

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var sgr = BuildSgrSequence(fg, bg);
        _cache[key] = sgr;
        return sgr;
    }

    private static int ColorToArgb(Color color)
    {
        return (color.R << 16) | (color.G << 8) | color.B;
    }

    private static string BuildSgrSequence(Color fg, Color bg)
    {
        return $"\x1b[38;2;{fg.R};{fg.G};{fg.B};48;2;{bg.R};{bg.G};{bg.B}m";
    }
}
