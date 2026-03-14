using System.Reflection;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using KustoTerminal.Driver.Platform;
using KustoTerminal.Driver.Rendering;

namespace KustoTerminal.Driver;

/// <summary>
/// High-performance console driver for Kusto Terminal.
/// Implements IConsoleDriver directly (cannot subclass ConsoleDriver because
/// GetParser() is internal abstract).
///
/// Key performance features:
/// - Differential rendering in UpdateScreen(): diff Contents vs front buffer
/// - Batched ANSI output: accumulate sequences, single write() per frame
/// - Synchronized output (DEC 2026): atomic frame updates, no flicker
/// - Direct fd I/O: bypass System.Console for raw read/write
/// - Raw stdin input parsing: lower-latency key detection
/// </summary>
public sealed class KustoConsoleDriver : IConsoleDriver
{
    private IPlatformTerminal? _terminal;
    private AnsiWriter? _ansiWriter;
    private Cell[,]? _frontBuffer;
    private bool _firstFrame = true;
    private CursorVisibility _cursorVisibility = CursorVisibility.Default;
    private bool _cursorVisible = true;
    private KustoMainLoopProxy? _loopProxy;

    // === IConsoleDriver Properties ===

    public int Cols { get; set; }
    public int Rows { get; set; }
    public int Col { get; private set; }
    public int Row { get; private set; }
    public int Left { get; set; }
    public int Top { get; set; }
    public System.Drawing.Rectangle Screen => new(0, 0, Cols, Rows);
    public Attribute CurrentAttribute { get; set; }
    public Cell[,] Contents { get; set; } = new Cell[0, 0];
    public Region Clip { get; set; } = new();
    public bool SupportsTrueColor => _terminal?.SupportsTrueColor ?? false;
    public bool Force16Colors { get; set; }
    public IClipboard Clipboard { get; } = new InMemoryClipboard();

    // === Events ===

    public event EventHandler<Key>? KeyDown;
    public event EventHandler<Key>? KeyUp;
    public event EventHandler<MouseEventArgs>? MouseEvent;
    public event EventHandler<SizeChangedEventArgs>? SizeChanged;
    public event EventHandler<EventArgs>? ClearedContents;

    // === Lifecycle ===

    public MainLoop Init()
    {
        _terminal = new UnixTerminal();

        var (cols, rows) = _terminal.GetTerminalSize();
        Cols = cols;
        Rows = rows;

        InitContents(cols, rows);

        CurrentAttribute = CreateAttributeDirect(Color.White, Color.Black);
        _ansiWriter = new AnsiWriter(_terminal);

        // Enter raw mode and setup terminal
        _terminal.EnterRawMode();

        _ansiWriter.EnterAlternateScreen();
        _ansiWriter.HideCursor();
        _ansiWriter.EnableMouse();
        _ansiWriter.EnableBracketedPaste();
        _ansiWriter.EnableKittyKeyboard();
        _ansiWriter.EnableModifyOtherKeys();
        _ansiWriter.ClearScreen();
        _ansiWriter.Flush();

        _firstFrame = true;

        // Register resize handler
        _terminal.OnResize((newCols, newRows) =>
        {
            Cols = newCols;
            Rows = newRows;
            _frontBuffer = null;
            _firstFrame = true;
            ClearContents();
            SizeChanged?.Invoke(this,
                new SizeChangedEventArgs(new System.Drawing.Size(newCols, newRows)));
        });

        // Create MainLoop via reflection + DispatchProxy (IMainLoopDriver is internal)
        var proxy = KustoMainLoopProxy.CreateProxy(this, _terminal);
        _loopProxy = proxy as KustoMainLoopProxy;
        var ctor = typeof(MainLoop).GetConstructors(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)[0];
        return (MainLoop)ctor.Invoke(new[] { proxy });
    }

    public void End()
    {
        if (_ansiWriter != null)
        {
            _ansiWriter.DisableModifyOtherKeys();
            _ansiWriter.DisableKittyKeyboard();
            _ansiWriter.DisableBracketedPaste();
            _ansiWriter.DisableMouse();
            _ansiWriter.ShowCursor();
            _ansiWriter.ResetAttributes();
            _ansiWriter.ExitAlternateScreen();
            _ansiWriter.Flush();
            _ansiWriter.Dispose();
        }

        _terminal?.ExitRawMode();
        _terminal?.Dispose();
    }

    public void Suspend()
    {
        if (_ansiWriter != null)
        {
            _ansiWriter.DisableModifyOtherKeys();
            _ansiWriter.DisableKittyKeyboard();
            _ansiWriter.DisableMouse();
            _ansiWriter.ShowCursor();
            _ansiWriter.ResetAttributes();
            _ansiWriter.ExitAlternateScreen();
            _ansiWriter.Flush();
        }
        _terminal?.ExitRawMode();
    }

    // === Drawing: Write to Contents buffer ===

    public void Move(int col, int row)
    {
        Col = col;
        Row = row;
    }

    public void AddRune(Rune rune)
    {
        if (Col >= 0 && Col < Cols && Row >= 0 && Row < Rows)
        {
            if (!Clip.Contains(Col, Row))
            {
                Col++;
                return;
            }

            Contents[Row, Col] = new Cell { Rune = rune, Attribute = CurrentAttribute };
        }
        Col++;
    }

    public void AddRune(char c) => AddRune(new Rune(c));

    public void AddStr(string str)
    {
        foreach (var rune in str.EnumerateRunes())
            AddRune(rune);
    }

    public Attribute SetAttribute(Attribute c)
    {
        var prev = CurrentAttribute;
        CurrentAttribute = c;
        return prev;
    }

    public Attribute GetAttribute() => CurrentAttribute;

    public Attribute MakeColor(in Color foreground, in Color background)
    {
        // MUST NOT call new Attribute(Color, Color) — that constructor calls
        // Application.Driver.MakeColor(), creating infinite recursion.
        // Use the internal (int platformColor, Color fg, Color bg) constructor via reflection.
        return CreateAttributeDirect(foreground, background);
    }

    public void ClearContents()
    {
        InitContents(Cols, Rows);
        ClearedContents?.Invoke(this, EventArgs.Empty);
    }

    public void FillRect(System.Drawing.Rectangle rect, Rune rune)
    {
        for (int row = rect.Y; row < rect.Y + rect.Height && row < Rows; row++)
            for (int col = rect.X; col < rect.X + rect.Width && col < Cols; col++)
            {
                if (!Clip.Contains(col, row)) continue;
                Contents[row, col] = new Cell { Rune = rune, Attribute = CurrentAttribute };
            }
    }

    public void FillRect(System.Drawing.Rectangle rect, char c) =>
        FillRect(rect, new Rune(c));

    // === Screen Update: Differential Rendering ===

    public void Refresh()
    {
        UpdateScreen();
    }

    private bool UpdateScreen()
    {
        if (_ansiWriter == null) return false;

        int rows = Contents.GetLength(0);
        int cols = Contents.GetLength(1);
        if (rows == 0 || cols == 0) return false;

        _ansiWriter.BeginSynchronizedUpdate();
        _ansiWriter.HideCursor();

        if (_firstFrame || _frontBuffer == null
            || _frontBuffer.GetLength(0) != rows
            || _frontBuffer.GetLength(1) != cols)
        {
            RenderFull(rows, cols);
            _firstFrame = false;
        }
        else
        {
            RenderDiff(rows, cols);
        }

        if (_cursorVisible && Col >= 0 && Col < cols && Row >= 0 && Row < rows)
        {
            _ansiWriter.MoveTo(Col, Row);
            _ansiWriter.ShowCursor();
        }

        _ansiWriter.EndSynchronizedUpdate();
        _ansiWriter.Flush();

        CopyToFrontBuffer(rows, cols);
        _ansiWriter.ResetState();
        return true;
    }

    private void RenderFull(int rows, int cols)
    {
        for (int row = 0; row < rows; row++)
        {
            _ansiWriter!.MoveTo(0, row);
            int lastFg = -1, lastBg = -1;

            for (int col = 0; col < cols; col++)
            {
                ref var cell = ref Contents[row, col];
                EmitCellColor(ref cell, ref lastFg, ref lastBg);
                _ansiWriter.WriteRune(cell.Rune);
            }
        }
    }

    private void RenderDiff(int rows, int cols)
    {
        for (int row = 0; row < rows; row++)
        {
            int col = 0;
            while (col < cols)
            {
                if (CellsEqual(ref _frontBuffer![row, col], ref Contents[row, col]))
                {
                    col++;
                    continue;
                }

                int runStart = col;
                while (col < cols && !CellsEqual(ref _frontBuffer![row, col], ref Contents[row, col]))
                    col++;

                _ansiWriter!.MoveTo(runStart, row);
                int lastFg = -1, lastBg = -1;
                for (int c = runStart; c < col; c++)
                {
                    ref var cell = ref Contents[row, c];
                    EmitCellColor(ref cell, ref lastFg, ref lastBg);
                    _ansiWriter.WriteRune(cell.Rune);
                }
            }
        }
    }

    private void EmitCellColor(ref Cell cell, ref int lastFg, ref int lastBg)
    {
        var attr = cell.Attribute ?? CurrentAttribute;
        int fg = (attr.Foreground.R << 16) | (attr.Foreground.G << 8) | attr.Foreground.B;
        int bg = (attr.Background.R << 16) | (attr.Background.G << 8) | attr.Background.B;

        if (fg != lastFg || bg != lastBg)
        {
            _ansiWriter!.SetColors(fg, bg);
            lastFg = fg;
            lastBg = bg;
        }
    }

    private void CopyToFrontBuffer(int rows, int cols)
    {
        if (_frontBuffer == null || _frontBuffer.GetLength(0) != rows || _frontBuffer.GetLength(1) != cols)
            _frontBuffer = new Cell[rows, cols];
        Array.Copy(Contents, _frontBuffer, Contents.Length);
    }

    private static bool CellsEqual(ref Cell a, ref Cell b)
    {
        if (a.Rune != b.Rune) return false;
        var attrA = a.Attribute;
        var attrB = b.Attribute;
        if (attrA == null && attrB == null) return true;
        if (attrA == null || attrB == null) return false;
        return attrA.Value.Foreground.R == attrB.Value.Foreground.R
            && attrA.Value.Foreground.G == attrB.Value.Foreground.G
            && attrA.Value.Foreground.B == attrB.Value.Foreground.B
            && attrA.Value.Background.R == attrB.Value.Background.R
            && attrA.Value.Background.G == attrB.Value.Background.G
            && attrA.Value.Background.B == attrB.Value.Background.B;
    }

    // === Cursor ===

    public void UpdateCursor()
    {
        if (_ansiWriter == null) return;
        if (_cursorVisible && Col >= 0 && Col < Cols && Row >= 0 && Row < Rows)
        {
            _ansiWriter.MoveTo(Col, Row);
            _ansiWriter.ShowCursor();
        }
        else
            _ansiWriter.HideCursor();
        _ansiWriter.Flush();
    }

    public bool GetCursorVisibility(out CursorVisibility visibility)
    {
        visibility = _cursorVisibility;
        return true;
    }

    public bool SetCursorVisibility(CursorVisibility visibility)
    {
        _cursorVisibility = visibility;
        _cursorVisible = visibility != CursorVisibility.Invisible;
        if (_ansiWriter != null)
        {
            if (_cursorVisible) _ansiWriter.ShowCursor();
            else _ansiWriter.HideCursor();
            _ansiWriter.Flush();
        }
        return true;
    }

    // === Raw Output ===

    public void WriteRaw(string ansi)
    {
        _ansiWriter?.WriteRawString(ansi);
        _ansiWriter?.Flush();
    }

    // === ANSI Request Handling ===

    public AnsiRequestScheduler GetRequestScheduler() =>
        new(new SimpleAnsiResponseParser());

    public void QueueAnsiRequest(AnsiEscapeSequenceRequest request) { }

    // === SendKeys ===

    public void SendKeys(char keyChar, ConsoleKey key, bool shift, bool alt, bool ctrl)
    {
        KeyCode kc = (KeyCode)keyChar;
        if (shift) kc |= KeyCode.ShiftMask;
        if (alt) kc |= KeyCode.AltMask;
        if (ctrl) kc |= KeyCode.CtrlMask;
        var k = new Key(kc);
        KeyDown?.Invoke(this, k);
        KeyUp?.Invoke(this, k);
    }

    // === Validation ===

    public bool IsRuneSupported(Rune rune) => true;
    public bool IsValidLocation(Rune rune, int col, int row) =>
        col >= 0 && col < Cols && row >= 0 && row < Rows;

    public string GetVersionInfo() =>
        "KustoConsoleDriver v1.0 (double-buffered differential ANSI)";

    // === Internal: Event raising (called by MainLoopProxy) ===

    internal void RaiseKeyDown(Key key) => KeyDown?.Invoke(this, key);
    internal void RaiseKeyUp(Key key) => KeyUp?.Invoke(this, key);
    internal void RaiseMouseEvent(MouseEventArgs args) => MouseEvent?.Invoke(this, args);

    /// <summary>
    /// Wake the event loop immediately (thread-safe).
    /// Call from timer callbacks or background threads to trigger a redraw
    /// without waiting for the poll timeout.
    /// </summary>
    public void Wakeup() => _loopProxy?.SignalWakeup();

    // === Helpers ===

    private void InitContents(int cols, int rows)
    {
        Contents = new Cell[rows, cols];
        var spaceRune = new Rune(' ');
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                Contents[r, c] = new Cell { Rune = spaceRune, Attribute = CurrentAttribute };
        Clip = new Region(new System.Drawing.Rectangle(0, 0, cols, rows));
    }

    /// <summary>Minimal IAnsiResponseParser (the concrete AnsiResponseParser is internal).</summary>
    private sealed class SimpleAnsiResponseParser : IAnsiResponseParser
    {
        public AnsiResponseParserState State => AnsiResponseParserState.Normal;
        public void ExpectResponse(string? terminator, Action<string?> response,
            Action? abandoned, bool persistent = false) { }
        public bool IsExpecting(string? terminator) => false;
        public void StopExpecting(string? requestTerminator, bool persistent = false) { }
    }

    /// <summary>In-memory clipboard implementation.</summary>
    private sealed class InMemoryClipboard : IClipboard
    {
        private string _content = string.Empty;
        public string GetClipboardData() => _content;
        public void SetClipboardData(string text) => _content = text ?? string.Empty;
        public bool IsSupported => true;
        public bool TryGetClipboardData(out string result) { result = _content; return true; }
        public bool TrySetClipboardData(string text) { _content = text ?? string.Empty; return true; }
    }

    // Cache the internal Attribute constructor to avoid repeated reflection
    private static readonly ConstructorInfo s_attrCtor = typeof(Attribute)
        .GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(int).MakeByRefType(), typeof(Color).MakeByRefType(), typeof(Color).MakeByRefType() },
            null)!;

    /// <summary>
    /// Create an Attribute without triggering the MakeColor recursion.
    /// Uses the internal (int, Color, Color) constructor.
    /// </summary>
    private static Attribute CreateAttributeDirect(Color foreground, Color background)
    {
        int platformColor = 0;
        object[] args = { platformColor, foreground, background };
        return (Attribute)s_attrCtor.Invoke(args);
    }
}
