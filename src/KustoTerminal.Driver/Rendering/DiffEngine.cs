using Terminal.Gui;
using Terminal.Gui.Drawing;

namespace KustoTerminal.Driver.Rendering;

/// <summary>
/// Cell-level differential rendering engine.
/// Compares the front (on-screen) and back (desired) buffers,
/// emitting only the minimal ANSI sequences needed to update the terminal.
/// This is the core performance optimization — inspired by Claude CLI's
/// differential rendering approach.
/// </summary>
internal static class DiffEngine
{
    /// <summary>
    /// Compare front and back buffers, emit ANSI sequences for differences.
    /// Returns the number of cells that were actually updated.
    /// </summary>
    public static int RenderDiff(ScreenBuffer screen, AnsiWriter writer)
    {
        int cols = screen.Cols;
        int rows = screen.Rows;
        int updatedCells = 0;

        for (int row = 0; row < rows; row++)
        {
            // Skip clean lines entirely
            if (!screen.IsLineDirty(row)) continue;

            int col = 0;
            while (col < cols)
            {
                // Skip identical cells
                ref var frontCell = ref screen.Front[row, col];
                ref var backCell = ref screen.Back[row, col];

                if (CellsEqual(ref frontCell, ref backCell))
                {
                    col++;
                    continue;
                }

                // Found a changed cell — scan for consecutive changed cells (run)
                int runStart = col;
                while (col < cols && !CellsEqual(ref screen.Front[row, col], ref screen.Back[row, col]))
                {
                    col++;
                }

                // Emit the run
                EmitRun(screen, writer, row, runStart, col);
                updatedCells += (col - runStart);
            }
        }

        return updatedCells;
    }

    /// <summary>
    /// Force a full redraw of the entire screen (used on first render or resize).
    /// </summary>
    public static void RenderFull(ScreenBuffer screen, AnsiWriter writer)
    {
        int cols = screen.Cols;
        int rows = screen.Rows;

        for (int row = 0; row < rows; row++)
        {
            writer.MoveTo(0, row);
            Attribute? lastAttr = null;

            for (int col = 0; col < cols; col++)
            {
                ref var cell = ref screen.Back[row, col];
                var attr = cell.Attribute;

                if (lastAttr == null || !AttributesEqual(attr, lastAttr.Value))
                {
                    EmitAttribute(writer, attr);
                    lastAttr = attr;
                }

                writer.WriteRune(cell.Rune);
            }
        }
    }

    private static void EmitRun(ScreenBuffer screen, AnsiWriter writer, int row, int startCol, int endCol)
    {
        writer.MoveTo(startCol, row);
        Attribute? lastAttr = null;

        for (int col = startCol; col < endCol; col++)
        {
            ref var cell = ref screen.Back[row, col];
            var attr = cell.Attribute;

            if (lastAttr == null || !AttributesEqual(attr, lastAttr.Value))
            {
                EmitAttribute(writer, attr);
                lastAttr = attr;
            }

            writer.WriteRune(cell.Rune);
        }
    }

    private static void EmitAttribute(AnsiWriter writer, Attribute? attr)
    {
        if (attr == null) return;
        var a = attr.Value;
        var fg = a.Foreground;
        var bg = a.Background;

        int fgRgb = (fg.R << 16) | (fg.G << 8) | fg.B;
        int bgRgb = (bg.R << 16) | (bg.G << 8) | bg.B;
        writer.SetColors(fgRgb, bgRgb);
    }

    private static bool CellsEqual(ref Cell a, ref Cell b)
    {
        return a.Rune == b.Rune && AttributesEqual(a.Attribute, b.Attribute);
    }

    private static bool AttributesEqual(Attribute? a, Attribute? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.Value.Foreground.R == b.Value.Foreground.R
            && a.Value.Foreground.G == b.Value.Foreground.G
            && a.Value.Foreground.B == b.Value.Foreground.B
            && a.Value.Background.R == b.Value.Background.R
            && a.Value.Background.G == b.Value.Background.G
            && a.Value.Background.B == b.Value.Background.B;
    }
}
