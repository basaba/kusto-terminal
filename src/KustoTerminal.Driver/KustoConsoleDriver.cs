using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using KustoTerminal.Driver.Output;
using KustoTerminal.Driver.Platform;
using TguiAttribute = Terminal.Gui.Drawing.Attribute;
using TguiColor = Terminal.Gui.Drawing.Color;
using Rectangle = System.Drawing.Rectangle;
using Size = System.Drawing.Size;

namespace KustoTerminal.Driver;

/// <summary>
/// High-performance Terminal.Gui console driver that implements IConsoleDriver directly.
/// Uses direct ANSI escape sequences with double-buffer diff rendering — only emits
/// escape sequences for cells that actually changed between frames.
///
/// Inspired by the rendering approaches used in Claude CLI and GitHub Copilot CLI.
/// </summary>
public sealed class KustoConsoleDriver : IConsoleDriver, IConsoleDriverFacade
{
    private readonly IPlatformTerminal _platform;
    private readonly DiffRenderer _diffRenderer = new();
    private readonly AnsiOutputBuffer _ansiBuffer = new();
    private readonly OutputBuffer _outputBuffer = new();

    private Thread? _inputThread;
    private CancellationTokenSource? _inputCts;
    private readonly ConcurrentQueue<ConsoleKeyInfo> _inputQueue = new();
    private IInputProcessor? _inputProcessor;

    #pragma warning disable CS0067 // MouseEvent is needed by IConsoleDriver but not yet used
    public event EventHandler<MouseEventArgs>? MouseEvent;
    #pragma warning restore CS0067

    private bool _cursorVisible = true;
    private bool _initialized;
    private bool _needsFullRedraw = true;
    private int _terminalRows;
    private int _terminalCols;

    public KustoConsoleDriver() : this(PlatformTerminalFactory.Create())
    {
    }

    public KustoConsoleDriver(IPlatformTerminal platform)
    {
        _platform = platform;
    }

    // --- IConsoleDriverFacade ---
    public IInputProcessor InputProcessor => _inputProcessor!;

    // --- IConsoleDriver Properties (delegated to OutputBuffer) ---

    public Cell[,]? Contents
    {
        get => _outputBuffer.Contents;
        set => _outputBuffer.Contents = value!;
    }

    public Region? Clip
    {
        get => _outputBuffer.Clip;
        set => _outputBuffer.Clip = value!;
    }

    public TguiAttribute CurrentAttribute
    {
        get => _outputBuffer.CurrentAttribute;
        set => _outputBuffer.CurrentAttribute = value;
    }

    public int Rows
    {
        get => _outputBuffer.Rows;
        set => _outputBuffer.Rows = value;
    }

    public int Cols
    {
        get => _outputBuffer.Cols;
        set => _outputBuffer.Cols = value;
    }

    public int Row => _outputBuffer.Row;
    public int Col => _outputBuffer.Col;

    public int Left
    {
        get => _outputBuffer.Left;
        set => _outputBuffer.Left = value;
    }

    public int Top
    {
        get => _outputBuffer.Top;
        set => _outputBuffer.Top = value;
    }

    public Rectangle Screen => new(0, 0, Cols, Rows);

    public bool SupportsTrueColor => true;

    public bool Force16Colors { get; set; }

    public IClipboard Clipboard { get; private set; } = new InternalClipboard();

    // --- Events ---
    public event EventHandler<SizeChangedEventArgs>? SizeChanged;
    public event EventHandler<EventArgs>? ClearedContents;
    public event EventHandler<Key>? KeyDown;
    public event EventHandler<Key>? KeyUp;

    // --- IConsoleDriver Methods (Buffer operations delegated to OutputBuffer) ---

    public void AddRune(Rune rune) => _outputBuffer.AddRune(rune);
    public void AddRune(char c) => _outputBuffer.AddRune(c);
    public void AddStr(string str) => _outputBuffer.AddStr(str);
    public void Move(int col, int row) => _outputBuffer.Move(col, row);
    public void FillRect(Rectangle rect, Rune rune) => _outputBuffer.FillRect(rect, rune);
    public void FillRect(Rectangle rect, char c) => _outputBuffer.FillRect(rect, c);
    public bool IsValidLocation(Rune rune, int col, int row) => _outputBuffer.IsValidLocation(rune, col, row);

    public TguiAttribute GetAttribute() => _outputBuffer.CurrentAttribute;

    public TguiAttribute SetAttribute(TguiAttribute c)
    {
        var prev = _outputBuffer.CurrentAttribute;
        _outputBuffer.CurrentAttribute = c;
        return prev;
    }

    // Cached reflection fields for constructing Attribute without triggering MakeColor recursion
    private static readonly System.Reflection.FieldInfo? s_fgField = typeof(TguiAttribute)
        .GetField("<Foreground>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    private static readonly System.Reflection.FieldInfo? s_bgField = typeof(TguiAttribute)
        .GetField("<Background>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    public TguiAttribute MakeColor(in TguiColor foreground, in TguiColor background)
    {
        // Cannot use new Attribute(Color, Color) — it calls Application.Driver.MakeColor() → infinite recursion.
        // Cannot use new Attribute() — its default ctor accesses Attribute.Default which also calls MakeColor.
        // Use default(TguiAttribute) to get zero-initialized struct with no ctor call, then set fields via reflection.
        var attr = default(TguiAttribute);
        if (s_fgField is not null && s_bgField is not null)
        {
            object boxed = attr;
            s_fgField.SetValue(boxed, foreground);
            s_bgField.SetValue(boxed, background);
            return (TguiAttribute)boxed;
        }
        return attr;
    }

    public bool IsRuneSupported(Rune rune) => true;

    public void ClearContents()
    {
        _outputBuffer.ClearContents();
        _needsFullRedraw = true;
        _diffRenderer.Invalidate();
        ClearedContents?.Invoke(this, EventArgs.Empty);
    }

    public AnsiRequestScheduler GetRequestScheduler()
    {
        return new AnsiRequestScheduler(new NoOpAnsiResponseParser());
    }

    public void QueueAnsiRequest(AnsiEscapeSequenceRequest request)
    {
        // No-op: we handle ANSI directly
    }

    // --- Lifecycle ---

    public MainLoop Init()
    {
        if (_initialized) throw new InvalidOperationException("Driver already initialized");

        // Get initial terminal size
        var (rows, cols) = _platform.GetWindowSize();
        _terminalRows = rows;
        _terminalCols = cols;
        _outputBuffer.SetWindowSize(cols, rows);

        // Enable raw mode for direct ANSI control
        _platform.EnableRawMode();

        // Switch to alternate screen buffer and set up terminal
        _ansiBuffer.Reset();
        _ansiBuffer.EnterAlternateScreen();
        _ansiBuffer.HideCursor();
        _ansiBuffer.EnableSgrMouseMode();
        _ansiBuffer.EnableBracketedPaste();
        _ansiBuffer.ClearScreen();
        _ansiBuffer.MoveTo(0, 0);
        _platform.WriteToStdout(_ansiBuffer.ToString());

        // Initialize content buffer
        ClearContents();

        // Register resize handler
        _platform.OnResize((newRows, newCols) =>
        {
            _terminalRows = newRows;
            _terminalCols = newCols;
            _outputBuffer.SetWindowSize(newCols, newRows);
            _needsFullRedraw = true;
            _diffRenderer.Invalidate();
            SizeChanged?.Invoke(this, new SizeChangedEventArgs(new Size(newCols, newRows)));
        });

        // Start input reading thread
        _inputCts = new CancellationTokenSource();
        _inputThread = new Thread(InputReadLoop)
        {
            Name = "KustoDriver-Input",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal
        };
        _inputThread.Start();

        _initialized = true;

        return CreateMainLoop();
    }

    public void End()
    {
        if (!_initialized) return;

        _inputCts?.Cancel();
        _inputThread?.Join(TimeSpan.FromSeconds(2));
        _inputCts?.Dispose();

        _ansiBuffer.Reset();
        _ansiBuffer.DisableSgrMouseMode();
        _ansiBuffer.DisableBracketedPaste();
        _ansiBuffer.ShowCursor();
        _ansiBuffer.ResetAttributes();
        _ansiBuffer.LeaveAlternateScreen();
        _platform.WriteToStdout(_ansiBuffer.ToString());

        _platform.RestoreMode();
        _platform.Dispose();

        _initialized = false;
    }

    // --- Rendering (the performance-critical path) ---

    public void Refresh()
    {
        if (!_initialized || Contents is null) return;

        var rows = Rows;
        var cols = Cols;
        if (rows <= 0 || cols <= 0) return;

        string output;

        if (_needsFullRedraw)
        {
            output = _diffRenderer.RenderFull(Contents, rows, cols);
            _needsFullRedraw = false;
        }
        else
        {
            var dirtyLines = _outputBuffer.DirtyLines;
            if (dirtyLines is null)
            {
                var allDirty = new bool[rows];
                Array.Fill(allDirty, true);
                output = _diffRenderer.Render(Contents, allDirty, rows, cols);
            }
            else
            {
                output = _diffRenderer.Render(Contents, dirtyLines, rows, cols);
            }
        }

        if (output.Length > 0)
        {
            _platform.WriteToStdout(output);
        }

        UpdateCursor();
    }

    public void UpdateCursor()
    {
        if (!_initialized) return;

        _ansiBuffer.Reset();
        if (_cursorVisible && Col >= 0 && Row >= 0 && Col < Cols && Row < Rows)
        {
            _ansiBuffer.MoveTo(Row, Col);
            _ansiBuffer.ShowCursor();
        }
        else
        {
            _ansiBuffer.HideCursor();
        }
        _platform.WriteToStdout(_ansiBuffer.ToString());
    }

    public bool GetCursorVisibility(out CursorVisibility visibility)
    {
        visibility = _cursorVisible ? CursorVisibility.Default : CursorVisibility.Invisible;
        return true;
    }

    public bool SetCursorVisibility(CursorVisibility visibility)
    {
        _cursorVisible = visibility != CursorVisibility.Invisible;
        _ansiBuffer.Reset();
        if (_cursorVisible) _ansiBuffer.ShowCursor();
        else _ansiBuffer.HideCursor();
        _platform.WriteToStdout(_ansiBuffer.ToString());
        return true;
    }

    public void Suspend()
    {
        _ansiBuffer.Reset();
        _ansiBuffer.ShowCursor();
        _ansiBuffer.ResetAttributes();
        _ansiBuffer.LeaveAlternateScreen();
        _platform.WriteToStdout(_ansiBuffer.ToString());
        _platform.RestoreMode();

        _platform.EnableRawMode();
        _ansiBuffer.Reset();
        _ansiBuffer.EnterAlternateScreen();
        _ansiBuffer.EnableSgrMouseMode();
        _needsFullRedraw = true;
        _platform.WriteToStdout(_ansiBuffer.ToString());
    }

    public void SendKeys(char keyChar, ConsoleKey key, bool shift, bool alt, bool ctrl)
    {
        _inputQueue.Enqueue(new ConsoleKeyInfo(keyChar, key, shift, alt, ctrl));
    }

    public void WriteRaw(string ansi)
    {
        if (!_initialized) return;
        _platform.WriteToStdout(ansi);
    }

    public string GetVersionInfo() => "KustoConsoleDriver v1.0 (ANSI diff-renderer)";

    // --- Input Processing ---

    private void InputReadLoop()
    {
        var token = _inputCts!.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(intercept: true);
                    _inputQueue.Enqueue(keyInfo);
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }

    internal bool ProcessInput()
    {
        bool processed = false;

        while (_inputQueue.TryDequeue(out var keyInfo))
        {
            var key = MapConsoleKeyInfo(keyInfo);
            if (key != Key.Empty)
            {
                KeyDown?.Invoke(this, key);
                KeyUp?.Invoke(this, key);
                processed = true;
            }
        }

        var (rows, cols) = _platform.GetWindowSize();
        if (rows != _terminalRows || cols != _terminalCols)
        {
            _terminalRows = rows;
            _terminalCols = cols;
            _outputBuffer.SetWindowSize(cols, rows);
            _needsFullRedraw = true;
            _diffRenderer.Invalidate();
            ClearContents();
            SizeChanged?.Invoke(this, new SizeChangedEventArgs(new Size(cols, rows)));
            processed = true;
        }

        return processed;
    }

    private MainLoop CreateMainLoop()
    {
        var proxyDriver = KustoMainLoopDriver.Create(this);
        var ctor = typeof(MainLoop).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];
        return (MainLoop)ctor.Invoke(new object[] { proxyDriver });
    }

    private static Key MapConsoleKeyInfo(ConsoleKeyInfo keyInfo)
    {
        var key = keyInfo.Key switch
        {
            ConsoleKey.Escape => Key.Esc,
            ConsoleKey.Tab => Key.Tab,
            ConsoleKey.Backspace => Key.Backspace,
            ConsoleKey.Enter => Key.Enter,
            ConsoleKey.Delete => Key.Delete,
            ConsoleKey.Home => Key.Home,
            ConsoleKey.End => Key.End,
            ConsoleKey.PageUp => Key.PageUp,
            ConsoleKey.PageDown => Key.PageDown,
            ConsoleKey.UpArrow => Key.CursorUp,
            ConsoleKey.DownArrow => Key.CursorDown,
            ConsoleKey.LeftArrow => Key.CursorLeft,
            ConsoleKey.RightArrow => Key.CursorRight,
            ConsoleKey.Insert => Key.InsertChar,
            ConsoleKey.F1 => Key.F1, ConsoleKey.F2 => Key.F2, ConsoleKey.F3 => Key.F3,
            ConsoleKey.F4 => Key.F4, ConsoleKey.F5 => Key.F5, ConsoleKey.F6 => Key.F6,
            ConsoleKey.F7 => Key.F7, ConsoleKey.F8 => Key.F8, ConsoleKey.F9 => Key.F9,
            ConsoleKey.F10 => Key.F10, ConsoleKey.F11 => Key.F11, ConsoleKey.F12 => Key.F12,
            ConsoleKey.Spacebar => Key.Space,
            _ => Key.Empty
        };

        if (key == Key.Empty && keyInfo.KeyChar != '\0')
            key = (Key)keyInfo.KeyChar;

        if ((keyInfo.Modifiers & ConsoleModifiers.Shift) != 0) key = key.WithShift;
        if ((keyInfo.Modifiers & ConsoleModifiers.Alt) != 0) key = key.WithAlt;
        if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0) key = key.WithCtrl;

        return key;
    }
}

internal sealed class InternalClipboard : IClipboard
{
    private string _contents = string.Empty;
    public string GetClipboardData() => _contents;
    public void SetClipboardData(string text) => _contents = text ?? string.Empty;
    public bool IsSupported => true;
    public bool TryGetClipboardData(out string result) { result = _contents; return true; }
    public bool TrySetClipboardData(string text) { _contents = text ?? string.Empty; return true; }
}
