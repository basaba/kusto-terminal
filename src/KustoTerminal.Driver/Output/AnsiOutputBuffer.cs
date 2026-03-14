using System.Text;

namespace KustoTerminal.Driver.Output;

/// <summary>
/// Builds ANSI escape sequences into a StringBuilder for efficient batched terminal output.
/// All methods append to an internal buffer; call ToString() to get the final output and Reset() to clear.
/// </summary>
public sealed class AnsiOutputBuffer
{
    private readonly StringBuilder _buffer = new(4096);

    private const char Esc = '\x1b';
    private const string Csi = "\x1b[";

    public int Length => _buffer.Length;

    public void Reset() => _buffer.Clear();

    public override string ToString() => _buffer.ToString();

    /// <summary>Append raw text (no escape processing).</summary>
    public void AppendRaw(string text) => _buffer.Append(text);

    /// <summary>Append a single character.</summary>
    public void AppendRune(char c) => _buffer.Append(c);

    /// <summary>Append a Rune (may be multi-byte).</summary>
    public void AppendRune(Rune rune)
    {
        Span<char> chars = stackalloc char[2];
        int len = rune.EncodeToUtf16(chars);
        _buffer.Append(chars[..len]);
    }

    // --- Cursor Movement ---

    /// <summary>Move cursor to absolute position (1-based).</summary>
    public void MoveTo(int row, int col)
    {
        _buffer.Append(Csi);
        _buffer.Append(row + 1);
        _buffer.Append(';');
        _buffer.Append(col + 1);
        _buffer.Append('H');
    }

    /// <summary>Move cursor up by n rows.</summary>
    public void MoveUp(int n = 1)
    {
        if (n <= 0) return;
        _buffer.Append(Csi);
        if (n != 1) _buffer.Append(n);
        _buffer.Append('A');
    }

    /// <summary>Move cursor down by n rows.</summary>
    public void MoveDown(int n = 1)
    {
        if (n <= 0) return;
        _buffer.Append(Csi);
        if (n != 1) _buffer.Append(n);
        _buffer.Append('B');
    }

    /// <summary>Move cursor right by n columns.</summary>
    public void MoveRight(int n = 1)
    {
        if (n <= 0) return;
        _buffer.Append(Csi);
        if (n != 1) _buffer.Append(n);
        _buffer.Append('C');
    }

    /// <summary>Move cursor left by n columns.</summary>
    public void MoveLeft(int n = 1)
    {
        if (n <= 0) return;
        _buffer.Append(Csi);
        if (n != 1) _buffer.Append(n);
        _buffer.Append('D');
    }

    // --- Screen Control ---

    /// <summary>Clear entire screen.</summary>
    public void ClearScreen()
    {
        _buffer.Append(Csi);
        _buffer.Append("2J");
    }

    /// <summary>Clear from cursor to end of line.</summary>
    public void ClearToEndOfLine()
    {
        _buffer.Append(Csi);
        _buffer.Append('K');
    }

    /// <summary>Clear entire line.</summary>
    public void ClearLine()
    {
        _buffer.Append(Csi);
        _buffer.Append("2K");
    }

    // --- Cursor Visibility ---

    public void ShowCursor()
    {
        _buffer.Append(Csi);
        _buffer.Append("?25h");
    }

    public void HideCursor()
    {
        _buffer.Append(Csi);
        _buffer.Append("?25l");
    }

    /// <summary>Set cursor style (DECSCUSR). 0=default, 1=blinking block, 2=steady block, etc.</summary>
    public void SetCursorStyle(int style)
    {
        _buffer.Append(Csi);
        _buffer.Append(style);
        _buffer.Append(" q");
    }

    // --- Alternate Screen Buffer ---

    public void EnterAlternateScreen()
    {
        _buffer.Append(Csi);
        _buffer.Append("?1049h");
    }

    public void LeaveAlternateScreen()
    {
        _buffer.Append(Csi);
        _buffer.Append("?1049l");
    }

    // --- Scroll Regions ---

    /// <summary>Set scroll region (1-based). Pass 0,0 to reset.</summary>
    public void SetScrollRegion(int top, int bottom)
    {
        _buffer.Append(Csi);
        if (top > 0 && bottom > 0)
        {
            _buffer.Append(top);
            _buffer.Append(';');
            _buffer.Append(bottom);
        }
        _buffer.Append('r');
    }

    /// <summary>Scroll up by n lines within scroll region.</summary>
    public void ScrollUp(int n = 1)
    {
        if (n <= 0) return;
        _buffer.Append(Csi);
        if (n != 1) _buffer.Append(n);
        _buffer.Append('S');
    }

    /// <summary>Scroll down by n lines within scroll region.</summary>
    public void ScrollDown(int n = 1)
    {
        if (n <= 0) return;
        _buffer.Append(Csi);
        if (n != 1) _buffer.Append(n);
        _buffer.Append('T');
    }

    // --- SGR (Select Graphic Rendition) - Colors ---

    /// <summary>Reset all attributes to default.</summary>
    public void ResetAttributes()
    {
        _buffer.Append(Csi);
        _buffer.Append("0m");
    }

    /// <summary>Set 24-bit truecolor foreground.</summary>
    public void SetForegroundRgb(int r, int g, int b)
    {
        _buffer.Append(Csi);
        _buffer.Append("38;2;");
        _buffer.Append(r);
        _buffer.Append(';');
        _buffer.Append(g);
        _buffer.Append(';');
        _buffer.Append(b);
        _buffer.Append('m');
    }

    /// <summary>Set 24-bit truecolor background.</summary>
    public void SetBackgroundRgb(int r, int g, int b)
    {
        _buffer.Append(Csi);
        _buffer.Append("48;2;");
        _buffer.Append(r);
        _buffer.Append(';');
        _buffer.Append(g);
        _buffer.Append(';');
        _buffer.Append(b);
        _buffer.Append('m');
    }

    /// <summary>Set foreground and background in a single SGR sequence.</summary>
    public void SetColors(int fgR, int fgG, int fgB, int bgR, int bgG, int bgB)
    {
        _buffer.Append(Csi);
        _buffer.Append("38;2;");
        _buffer.Append(fgR);
        _buffer.Append(';');
        _buffer.Append(fgG);
        _buffer.Append(';');
        _buffer.Append(fgB);
        _buffer.Append(";48;2;");
        _buffer.Append(bgR);
        _buffer.Append(';');
        _buffer.Append(bgG);
        _buffer.Append(';');
        _buffer.Append(bgB);
        _buffer.Append('m');
    }

    // --- Mouse Modes ---

    /// <summary>Enable SGR mouse mode (most modern, supports coordinates > 223).</summary>
    public void EnableSgrMouseMode()
    {
        _buffer.Append(Csi);
        _buffer.Append("?1006h");
        _buffer.Append(Csi);
        _buffer.Append("?1003h"); // all motion events
    }

    /// <summary>Disable SGR mouse mode.</summary>
    public void DisableSgrMouseMode()
    {
        _buffer.Append(Csi);
        _buffer.Append("?1003l");
        _buffer.Append(Csi);
        _buffer.Append("?1006l");
    }

    // --- Bracketed Paste ---

    public void EnableBracketedPaste()
    {
        _buffer.Append(Csi);
        _buffer.Append("?2004h");
    }

    public void DisableBracketedPaste()
    {
        _buffer.Append(Csi);
        _buffer.Append("?2004l");
    }
}
