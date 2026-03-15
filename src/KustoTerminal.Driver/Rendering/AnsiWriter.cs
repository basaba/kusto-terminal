using System.Buffers;
using System.Text;
using KustoTerminal.Driver.Platform;

namespace KustoTerminal.Driver.Rendering;

/// <summary>
/// High-performance ANSI escape sequence writer.
/// Accumulates all output into a single byte buffer and flushes in one write() call.
/// Tracks current attribute state to skip redundant color changes.
/// Supports synchronized output (DEC 2026) for flicker-free rendering.
/// </summary>
internal sealed class AnsiWriter
{
    private readonly IPlatformTerminal _terminal;
    private byte[] _buffer;
    private int _length;

    // Track last emitted state to skip redundant sequences
    private int _lastFg = -1;
    private int _lastBg = -1;
    private int _lastCol = -1;
    private int _lastRow = -1;

    private const int InitialBufferSize = 64 * 1024; // 64KB initial
    private const int MaxBufferSize = 4 * 1024 * 1024; // 4MB max

    public AnsiWriter(IPlatformTerminal terminal)
    {
        _terminal = terminal;
        _buffer = ArrayPool<byte>.Shared.Rent(InitialBufferSize);
        _length = 0;
    }

    /// <summary>Reset tracked state (call after buffer swap)</summary>
    public void ResetState()
    {
        _lastFg = -1;
        _lastBg = -1;
        _lastCol = -1;
        _lastRow = -1;
    }

    /// <summary>Begin synchronized output frame (DEC 2026)</summary>
    public void BeginSynchronizedUpdate()
    {
        AppendLiteral("\x1b[?2026h"u8);
    }

    /// <summary>End synchronized output frame (DEC 2026)</summary>
    public void EndSynchronizedUpdate()
    {
        AppendLiteral("\x1b[?2026l"u8);
    }

    /// <summary>Move cursor to absolute position (1-based for ANSI)</summary>
    public void MoveTo(int col, int row)
    {
        if (col == _lastCol && row == _lastRow) return;

        // If just moving forward one column, no sequence needed (natural cursor advance)
        if (row == _lastRow && col == _lastCol + 1)
        {
            _lastCol = col;
            return;
        }

        _lastCol = col;
        _lastRow = row;

        // CUP: \x1b[{row+1};{col+1}H (ANSI is 1-based)
        EnsureCapacity(16);
        _buffer[_length++] = 0x1b;
        _buffer[_length++] = (byte)'[';
        AppendInt(row + 1);
        _buffer[_length++] = (byte)';';
        AppendInt(col + 1);
        _buffer[_length++] = (byte)'H';
    }

    /// <summary>Set foreground and background colors (24-bit true color)</summary>
    public void SetColors(int fg, int bg)
    {
        if (fg == _lastFg && bg == _lastBg) return;

        EnsureCapacity(48); // Worst case: two SGR sequences

        bool needSemicolon = false;

        _buffer[_length++] = 0x1b;
        _buffer[_length++] = (byte)'[';

        if (fg != _lastFg)
        {
            // \x1b[38;2;R;G;Bm
            _buffer[_length++] = (byte)'3';
            _buffer[_length++] = (byte)'8';
            _buffer[_length++] = (byte)';';
            _buffer[_length++] = (byte)'2';
            _buffer[_length++] = (byte)';';
            AppendInt((fg >> 16) & 0xFF);
            _buffer[_length++] = (byte)';';
            AppendInt((fg >> 8) & 0xFF);
            _buffer[_length++] = (byte)';';
            AppendInt(fg & 0xFF);
            _lastFg = fg;
            needSemicolon = true;
        }

        if (bg != _lastBg)
        {
            if (needSemicolon)
                _buffer[_length++] = (byte)';';
            // 48;2;R;G;B
            _buffer[_length++] = (byte)'4';
            _buffer[_length++] = (byte)'8';
            _buffer[_length++] = (byte)';';
            _buffer[_length++] = (byte)'2';
            _buffer[_length++] = (byte)';';
            AppendInt((bg >> 16) & 0xFF);
            _buffer[_length++] = (byte)';';
            AppendInt((bg >> 8) & 0xFF);
            _buffer[_length++] = (byte)';';
            AppendInt(bg & 0xFF);
            _lastBg = bg;
        }

        _buffer[_length++] = (byte)'m';
    }

    /// <summary>Set 16-color mode foreground and background</summary>
    public void SetColors16(int fgIndex, int bgIndex)
    {
        if (fgIndex == _lastFg && bgIndex == _lastBg) return;

        EnsureCapacity(16);
        _buffer[_length++] = 0x1b;
        _buffer[_length++] = (byte)'[';

        if (fgIndex != _lastFg)
        {
            // Standard: 30-37, bright: 90-97
            AppendInt(fgIndex < 8 ? 30 + fgIndex : 82 + fgIndex);
            _lastFg = fgIndex;
            if (bgIndex != _lastBg)
                _buffer[_length++] = (byte)';';
        }

        if (bgIndex != _lastBg)
        {
            AppendInt(bgIndex < 8 ? 40 + bgIndex : 92 + bgIndex);
            _lastBg = bgIndex;
        }

        _buffer[_length++] = (byte)'m';
    }

    /// <summary>Write a single rune at the current position (advances cursor)</summary>
    public void WriteRune(Rune rune)
    {
        EnsureCapacity(4);
        Span<byte> utf8 = stackalloc byte[4];
        rune.TryEncodeToUtf8(utf8, out int bytesWritten);
        if (bytesWritten > 0)
        {
            EnsureCapacity(bytesWritten);
            utf8[..bytesWritten].CopyTo(_buffer.AsSpan(_length));
            _length += bytesWritten;
        }

        // Track cursor advance (wide chars take 2 columns)
        _lastCol += rune.Utf16SequenceLength > 1 ? 2 : 1;
    }

    /// <summary>Write a string at current position</summary>
    public void WriteString(ReadOnlySpan<char> text)
    {
        EnsureCapacity(text.Length * 4);
        var bytesWritten = Encoding.UTF8.GetBytes(text, _buffer.AsSpan(_length));
        _length += bytesWritten;
        _lastCol += text.Length;
    }

    /// <summary>Write raw ANSI sequence</summary>
    public void WriteRaw(ReadOnlySpan<byte> data)
    {
        EnsureCapacity(data.Length);
        data.CopyTo(_buffer.AsSpan(_length));
        _length += data.Length;
        // Raw writes invalidate position tracking
        _lastCol = -1;
        _lastRow = -1;
    }

    /// <summary>Write raw string (for WriteRaw(string))</summary>
    public void WriteRawString(string ansi)
    {
        EnsureCapacity(ansi.Length * 2);
        var bytesWritten = Encoding.UTF8.GetBytes(ansi, _buffer.AsSpan(_length));
        _length += bytesWritten;
        _lastCol = -1;
        _lastRow = -1;
    }

    /// <summary>Hide cursor</summary>
    public void HideCursor() => AppendLiteral("\x1b[?25l"u8);

    /// <summary>Show cursor</summary>
    public void ShowCursor() => AppendLiteral("\x1b[?25h"u8);

    /// <summary>Enter alternate screen buffer</summary>
    public void EnterAlternateScreen() => AppendLiteral("\x1b[?1049h"u8);

    /// <summary>Exit alternate screen buffer</summary>
    public void ExitAlternateScreen() => AppendLiteral("\x1b[?1049l"u8);

    /// <summary>Enable SGR mouse tracking</summary>
    public void EnableMouse()
    {
        AppendLiteral("\x1b[?1000h"u8); // Enable mouse press/release
        AppendLiteral("\x1b[?1002h"u8); // Enable mouse movement while pressed
        AppendLiteral("\x1b[?1006h"u8); // SGR extended mouse mode
    }

    /// <summary>Disable mouse tracking</summary>
    public void DisableMouse()
    {
        AppendLiteral("\x1b[?1006l"u8);
        AppendLiteral("\x1b[?1002l"u8);
        AppendLiteral("\x1b[?1000l"u8);
    }

    /// <summary>Enable bracketed paste mode</summary>
    public void EnableBracketedPaste() => AppendLiteral("\x1b[?2004h"u8);

    /// <summary>Disable bracketed paste mode</summary>
    public void DisableBracketedPaste() => AppendLiteral("\x1b[?2004l"u8);

    /// <summary>
    /// Enable the kitty keyboard protocol (progressive enhancement level 1).
    /// Makes the terminal send CSI u sequences for modified keys,
    /// e.g. Shift+Enter → ESC[13;2u instead of bare \r.
    /// Supported by: kitty, iTerm2 3.5+, WezTerm, foot, ghostty.
    /// </summary>
    public void EnableKittyKeyboard() => AppendLiteral("\x1b[>1u"u8);

    /// <summary>Disable the kitty keyboard protocol (pop from stack)</summary>
    public void DisableKittyKeyboard() => AppendLiteral("\x1b[<u"u8);

    /// <summary>
    /// Enable xterm modifyOtherKeys mode 2 as fallback for terminals
    /// that don't support kitty protocol but do support modifyOtherKeys.
    /// Makes the terminal send CSI 27;modifier;codepoint ~ for modified keys.
    /// </summary>
    public void EnableModifyOtherKeys() => AppendLiteral("\x1b[>4;2m"u8);

    /// <summary>Disable xterm modifyOtherKeys mode</summary>
    public void DisableModifyOtherKeys() => AppendLiteral("\x1b[>4;0m"u8);

    /// <summary>Reset all attributes</summary>
    public void ResetAttributes()
    {
        AppendLiteral("\x1b[0m"u8);
        _lastFg = -1;
        _lastBg = -1;
    }

    /// <summary>Clear entire screen</summary>
    public void ClearScreen() => AppendLiteral("\x1b[2J"u8);

    /// <summary>Set scroll region (DECSTBM): CSI top+1 ; bottom+1 r (0-based input)</summary>
    public void SetScrollRegion(int top, int bottom)
    {
        EnsureCapacity(16);
        _buffer[_length++] = 0x1b;
        _buffer[_length++] = (byte)'[';
        AppendInt(top + 1);
        _buffer[_length++] = (byte)';';
        AppendInt(bottom + 1);
        _buffer[_length++] = (byte)'r';
        // Scroll region changes invalidate cursor position
        _lastCol = -1;
        _lastRow = -1;
    }

    /// <summary>Reset scroll region to full screen: CSI 1 ; rows r</summary>
    public void ResetScrollRegion(int totalRows)
    {
        EnsureCapacity(16);
        _buffer[_length++] = 0x1b;
        _buffer[_length++] = (byte)'[';
        _buffer[_length++] = (byte)'1';
        _buffer[_length++] = (byte)';';
        AppendInt(totalRows);
        _buffer[_length++] = (byte)'r';
        _lastCol = -1;
        _lastRow = -1;
    }

    /// <summary>Scroll up n lines within scroll region: CSI n S</summary>
    public void ScrollUp(int n)
    {
        EnsureCapacity(12);
        _buffer[_length++] = 0x1b;
        _buffer[_length++] = (byte)'[';
        AppendInt(n);
        _buffer[_length++] = (byte)'S';
        _lastCol = -1;
        _lastRow = -1;
    }

    /// <summary>Scroll down n lines within scroll region: CSI n T</summary>
    public void ScrollDown(int n)
    {
        EnsureCapacity(12);
        _buffer[_length++] = 0x1b;
        _buffer[_length++] = (byte)'[';
        AppendInt(n);
        _buffer[_length++] = (byte)'T';
        _lastCol = -1;
        _lastRow = -1;
    }

    /// <summary>Flush accumulated buffer to terminal in a single write</summary>
    public void Flush()
    {
        if (_length == 0) return;
        _terminal.Write(_buffer.AsSpan(0, _length));
        _length = 0;
    }

    /// <summary>Discard accumulated buffer without writing</summary>
    public void Discard()
    {
        _length = 0;
    }

    /// <summary>Current buffer size</summary>
    public int Length => _length;

    private void AppendLiteral(ReadOnlySpan<byte> literal)
    {
        EnsureCapacity(literal.Length);
        literal.CopyTo(_buffer.AsSpan(_length));
        _length += literal.Length;
    }

    private void AppendInt(int value)
    {
        if (value < 10)
        {
            _buffer[_length++] = (byte)('0' + value);
            return;
        }
        if (value < 100)
        {
            _buffer[_length++] = (byte)('0' + value / 10);
            _buffer[_length++] = (byte)('0' + value % 10);
            return;
        }
        if (value < 1000)
        {
            _buffer[_length++] = (byte)('0' + value / 100);
            _buffer[_length++] = (byte)('0' + (value / 10) % 10);
            _buffer[_length++] = (byte)('0' + value % 10);
            return;
        }

        // General case
        Span<byte> digits = stackalloc byte[10];
        int pos = digits.Length;
        do
        {
            digits[--pos] = (byte)('0' + value % 10);
            value /= 10;
        } while (value > 0);

        var slice = digits[pos..];
        slice.CopyTo(_buffer.AsSpan(_length));
        _length += slice.Length;
    }

    private void EnsureCapacity(int additionalBytes)
    {
        if (_length + additionalBytes <= _buffer.Length) return;

        var newSize = Math.Min(
            Math.Max(_buffer.Length * 2, _length + additionalBytes),
            MaxBufferSize);

        var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
        _buffer.AsSpan(0, _length).CopyTo(newBuffer);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = newBuffer;
    }

    public void Dispose()
    {
        if (_buffer.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = Array.Empty<byte>();
        }
    }
}
