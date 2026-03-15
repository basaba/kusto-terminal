using System.Diagnostics;
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

    // Frame rate cap: prevent overwhelming the terminal emulator during fast scroll
    private long _lastRenderTicks;
    private const long MinRenderIntervalTicks = 8 * TimeSpan.TicksPerMillisecond; // ~120fps

    // When true, a Refresh was skipped by the rate cap and needs to be retried.
    // DoEventsPending uses this to return true after its poll timeout so the
    // main loop iterates again and Terminal.Gui calls Refresh.
    internal bool RefreshPending { get; private set; }

    // Optional performance stats (null when disabled, zero-cost check)
    private RenderStats? _renderStats;
    private string? _renderStatsPath;

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
        // Frame rate cap: skip if the last render was too recent.
        long now = Stopwatch.GetTimestamp();
        if (now - _lastRenderTicks < MinRenderIntervalTicks)
        {
            _renderStats?.RecordSkipped();
            RefreshPending = true;
            return;
        }

        RefreshPending = false;
        if (UpdateScreen())
            _lastRenderTicks = now;
    }

    private bool UpdateScreen()
    {
        if (_ansiWriter == null) return false;

        int rows = Contents.GetLength(0);
        int cols = Contents.GetLength(1);
        if (rows == 0 || cols == 0) return false;

        // Reset tracking at frame start so stale state from UpdateCursor()
        // (called between frames) doesn't corrupt MoveTo optimizations.
        _ansiWriter.ResetState();

        // Stamp FPS stats timing start
        long frameStart = _renderStats != null ? Stopwatch.GetTimestamp() : 0;
        string renderMode = "full";

        _ansiWriter.BeginSynchronizedUpdate();
        _ansiWriter.HideCursor();

        if (_firstFrame || _frontBuffer == null
            || _frontBuffer.GetLength(0) != rows
            || _frontBuffer.GetLength(1) != cols)
        {
            RenderFull(rows, cols);
            renderMode = "full";
            _firstFrame = false;
        }
        else
        {
            RenderDiff(rows, cols);
            renderMode = "diff";
        }

        if (_cursorVisible && Col >= 0 && Col < cols && Row >= 0 && Row < rows)
        {
            _ansiWriter.MoveTo(Col, Row);
            _ansiWriter.ShowCursor();
        }

        int ansiBytes = _ansiWriter.Length;

        _ansiWriter.EndSynchronizedUpdate();
        _ansiWriter.Flush();

        CopyToFrontBuffer(rows, cols);

        if (_renderStats != null)
        {
            long frameEnd = Stopwatch.GetTimestamp();
            int cellsEstimate = Math.Max(0, ansiBytes / 4);
            _renderStats.RecordFrame(frameStart, frameEnd, cellsEstimate, ansiBytes, renderMode);

            // Flush stats to file every 60 frames (~1-2 seconds)
            // so stats survive even an unclean exit
            if (_renderStatsPath != null && _renderStats.TotalFrames % 60 == 0)
            {
                try { File.WriteAllText(_renderStatsPath, _renderStats.FormatSummary()); }
                catch { }
            }
        }

        return true;
    }

    private void RenderFull(int rows, int cols)
    {
        for (int row = 0; row < rows; row++)
        {
            _ansiWriter!.MoveTo(0, row);
            int lastFg = -1, lastBg = -1, lastStyle = -1;

            for (int col = 0; col < cols; col++)
            {
                ref var cell = ref Contents[row, col];
                EmitCellAttributes(ref cell, ref lastFg, ref lastBg, ref lastStyle);
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
                int lastFg = -1, lastBg = -1, lastStyle = -1;
                for (int c = runStart; c < col; c++)
                {
                    ref var cell = ref Contents[row, c];
                    EmitCellAttributes(ref cell, ref lastFg, ref lastBg, ref lastStyle);
                    _ansiWriter.WriteRune(cell.Rune);
                }
            }
        }
    }

    /// <summary>
    /// Detect if Contents is a vertical shift of _frontBuffer.
    /// Returns positive for scroll-up (content moved up), negative for scroll-down, 0 if not a scroll.
    /// Only checks deltas 1–10 and requires ≥75% of shifted rows to match.
    /// </summary>
    private int DetectScrollDelta(int rows, int cols)
    {
        int maxDelta = Math.Min(10, rows / 2);
        for (int delta = 1; delta <= maxDelta; delta++)
        {
            // Scroll UP: Contents[r] should match _frontBuffer[r + delta]
            int matchUp = 0;
            for (int r = 0; r < rows - delta; r++)
                if (RowsMatch(r, r + delta, cols)) matchUp++;
            if (matchUp >= (rows - delta) * 3 / 4) return delta;

            // Scroll DOWN: Contents[r] should match _frontBuffer[r - delta]
            int matchDown = 0;
            for (int r = delta; r < rows; r++)
                if (RowsMatch(r, r - delta, cols)) matchDown++;
            if (matchDown >= (rows - delta) * 3 / 4) return -delta;
        }
        return 0;
    }

    /// <summary>Check if Contents[contentsRow] matches _frontBuffer[frontRow].</summary>
    private bool RowsMatch(int contentsRow, int frontRow, int cols)
    {
        for (int c = 0; c < cols; c++)
            if (!CellsEqual(ref Contents[contentsRow, c], ref _frontBuffer![frontRow, c]))
                return false;
        return true;
    }

    /// <summary>
    /// Scroll-optimized rendering: use ANSI scroll region commands to shift
    /// content in the terminal, then only repaint the newly exposed rows
    /// and any rows that don't match the expected shifted content.
    /// Reduces ANSI output from ~250KB (full repaint) to ~5-10KB.
    /// </summary>
    private void RenderScroll(int rows, int cols, int delta)
    {
        // Find the contiguous range of rows that shifted (skip static UI chrome)
        int scrollTop, scrollBottom;
        FindScrollRegion(rows, cols, delta, out scrollTop, out scrollBottom);

        // Use ANSI scroll region: CSI top ; bottom r
        _ansiWriter!.SetScrollRegion(scrollTop, scrollBottom);

        if (delta > 0)
        {
            // Content scrolled up: use CSI n S (scroll up)
            _ansiWriter.MoveTo(0, scrollBottom);
            _ansiWriter.ScrollUp(delta);
        }
        else
        {
            // Content scrolled down: use CSI n T (scroll down)
            _ansiWriter.MoveTo(0, scrollTop);
            _ansiWriter.ScrollDown(-delta);
        }

        // Reset scroll region to full screen
        _ansiWriter.ResetScrollRegion(rows);

        // Repaint the newly exposed rows
        if (delta > 0)
        {
            // Scrolled up: new content at the bottom
            for (int r = scrollBottom - delta + 1; r <= scrollBottom; r++)
                RenderRow(r, cols);
        }
        else
        {
            // Scrolled down: new content at the top
            for (int r = scrollTop; r < scrollTop + (-delta); r++)
                RenderRow(r, cols);
        }

        // Repaint any non-scrolling rows that changed (e.g., line numbers, status)
        for (int r = 0; r < rows; r++)
        {
            // Skip rows that were handled by the scroll
            if (r >= scrollTop && r <= scrollBottom) continue;

            // Check if this row changed
            bool changed = false;
            for (int c = 0; c < cols; c++)
            {
                if (!CellsEqual(ref _frontBuffer![r, c], ref Contents[r, c]))
                {
                    changed = true;
                    break;
                }
            }
            if (changed) RenderRow(r, cols);
        }
    }

    /// <summary>Find the scroll region bounds by detecting which rows actually shifted.</summary>
    private void FindScrollRegion(int rows, int cols, int delta, out int top, out int bottom)
    {
        // Find the first and last rows that participate in the scroll
        top = 0;
        bottom = rows - 1;

        if (delta > 0)
        {
            // Scroll up: find first row where Contents[r] == _frontBuffer[r + delta]
            while (top < rows - delta && !RowsMatch(top, top + delta, cols)) top++;
            // Find last row in the shifted region
            bottom = rows - 1;
            while (bottom > top && bottom - delta >= 0 && !RowsMatch(bottom - delta, bottom, cols)) bottom--;
        }
        else
        {
            int absDelta = -delta;
            // Scroll down: find first row where Contents[r] == _frontBuffer[r - delta]
            while (top + absDelta < rows && !RowsMatch(top + absDelta, top, cols)) top++;
            bottom = rows - 1;
            while (bottom > top + absDelta && !RowsMatch(bottom, bottom - absDelta, cols)) bottom--;
        }

        // Sanity: ensure valid range
        if (top > bottom) { top = 0; bottom = rows - 1; }
    }

    /// <summary>Render a single row from the Contents buffer.</summary>
    private void RenderRow(int row, int cols)
    {
        _ansiWriter!.MoveTo(0, row);
        int lastFg = -1, lastBg = -1, lastStyle = -1;
        for (int col = 0; col < cols; col++)
        {
            ref var cell = ref Contents[row, col];
            EmitCellAttributes(ref cell, ref lastFg, ref lastBg, ref lastStyle);
            _ansiWriter.WriteRune(cell.Rune);
        }
    }

    private void EmitCellAttributes(ref Cell cell, ref int lastFg, ref int lastBg, ref int lastStyle)
    {
        var attr = cell.Attribute ?? CurrentAttribute;
        int fg = (attr.Foreground.R << 16) | (attr.Foreground.G << 8) | attr.Foreground.B;
        int bg = (attr.Background.R << 16) | (attr.Background.G << 8) | attr.Background.B;
        int style = (int)attr.Style;

        if (style != lastStyle)
        {
            // Reset all attributes first, then re-apply colors + new style.
            // This is the safest way to handle style transitions.
            _ansiWriter!.ResetAttributes();
            lastFg = -1;
            lastBg = -1;
            lastStyle = style;

            if (style != 0)
                _ansiWriter.SetStyle(style);
        }

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
        return attrA.Value.Style == attrB.Value.Style
            && attrA.Value.Foreground.R == attrB.Value.Foreground.R
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

    /// <summary>Enable FPS/render performance tracking.</summary>
    public void EnableRenderStats()
    {
        _renderStats = new RenderStats();
        _renderStatsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".kusto-terminal-fps.log");
        Console.Error.WriteLine($"[FPS] Render stats enabled → {_renderStatsPath}");
    }

    /// <summary>Get render stats summary, or null if stats not enabled or no frames recorded.</summary>
    public string? GetRenderStatsSummary() =>
        _renderStats is { TotalFrames: > 0 } ? _renderStats.FormatSummary() : null;

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
        int platformColor = -1;
        object[] args = { platformColor, foreground, background };
        return (Attribute)s_attrCtor.Invoke(args);
    }
}
