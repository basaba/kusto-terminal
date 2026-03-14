using Terminal.Gui;
using Terminal.Gui.Drawing;

namespace KustoTerminal.Driver.Rendering;

/// <summary>
/// Double-buffered screen model.
/// Front buffer = what the terminal currently displays.
/// Back buffer = what we're building for the next frame.
/// Dirty line tracking enables fast skip of unchanged rows during diff.
/// </summary>
internal sealed class ScreenBuffer
{
    private Cell[,] _front;
    private Cell[,] _back;
    private bool[] _dirtyLines;
    private int _cols;
    private int _rows;

    public int Cols => _cols;
    public int Rows => _rows;

    /// <summary>
    /// The back buffer — Terminal.Gui writes to this via Contents property.
    /// </summary>
    public Cell[,] Back => _back;

    /// <summary>
    /// The front buffer — represents what's currently on screen.
    /// </summary>
    public Cell[,] Front => _front;

    public ScreenBuffer(int cols, int rows)
    {
        _cols = cols;
        _rows = rows;
        _front = new Cell[rows, cols];
        _back = new Cell[rows, cols];
        _dirtyLines = new bool[rows];

        ClearBuffer(_front, cols, rows);
        ClearBuffer(_back, cols, rows);
        Array.Fill(_dirtyLines, true); // Initial render: all dirty
    }

    /// <summary>Resize both buffers. Marks everything dirty.</summary>
    public void Resize(int cols, int rows)
    {
        if (cols == _cols && rows == _rows) return;

        _cols = cols;
        _rows = rows;
        _front = new Cell[rows, cols];
        _back = new Cell[rows, cols];
        _dirtyLines = new bool[rows];

        ClearBuffer(_front, cols, rows);
        ClearBuffer(_back, cols, rows);
        Array.Fill(_dirtyLines, true);
    }

    /// <summary>Set a cell in the back buffer</summary>
    public void SetCell(int col, int row, Rune rune, Attribute attr)
    {
        if (row < 0 || row >= _rows || col < 0 || col >= _cols) return;

        _back[row, col] = new Cell
        {
            Rune = rune,
            Attribute = attr
        };
        _dirtyLines[row] = true;
    }

    /// <summary>Check if a line is potentially dirty</summary>
    public bool IsLineDirty(int row)
    {
        return row >= 0 && row < _rows && _dirtyLines[row];
    }

    /// <summary>
    /// After flush: copy back → front so front reflects what's on screen.
    /// Reset dirty flags.
    /// </summary>
    public void SwapBuffers()
    {
        Array.Copy(_back, _front, _back.Length);
        Array.Fill(_dirtyLines, false);
    }

    /// <summary>Clear the back buffer with spaces and default attribute</summary>
    public void ClearBack()
    {
        ClearBuffer(_back, _cols, _rows);
        Array.Fill(_dirtyLines, true);
    }

    /// <summary>Mark all lines dirty (forces full redraw)</summary>
    public void MarkAllDirty()
    {
        Array.Fill(_dirtyLines, true);
    }

    /// <summary>Mark a specific line dirty</summary>
    public void MarkLineDirty(int row)
    {
        if (row >= 0 && row < _rows)
            _dirtyLines[row] = true;
    }

    private static void ClearBuffer(Cell[,] buffer, int cols, int rows)
    {
        var spaceRune = new Rune(' ');
        var defaultAttr = default(Attribute);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                buffer[r, c] = new Cell
                {
                    Rune = spaceRune,
                    Attribute = defaultAttr
                };
            }
        }
    }
}
