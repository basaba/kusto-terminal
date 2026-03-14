# Custom Console Driver Integration Guide

## Overview

Terminal.Gui 2.0 allows you to implement custom console drivers by implementing the `IConsoleDriver` interface. This guide explains how to create and register a custom driver in the KustoTerminal application.

---

## Step 1: Implement IConsoleDriver Interface

Use the `CustomConsoleDriver_Template.cs` file as a starting point. The interface requires:

### 13 Properties
- `Clipboard`, `Screen`, `Clip`, `Col`, `Cols`, `Contents`
- `Left`, `Row`, `Rows`, `Top`
- `SupportsTrueColor`, `Force16Colors`, `CurrentAttribute`

### 25 Methods (including overloads)
- **Lifecycle**: `Init()`, `End()`, `Suspend()`
- **Rendering**: `Move()`, `AddRune()`, `AddStr()`, `FillRect()`, `ClearContents()`, `Refresh()`, `UpdateScreen()`, `UpdateCursor()`
- **Colors**: `SetAttribute()`, `GetAttribute()`, `MakeColor()`
- **Cursor**: `GetCursorVisibility()`, `SetCursorVisibility()`
- **Validation**: `IsRuneSupported()`, `IsValidLocation()`, `GetVersionInfo()`, `WriteRaw()`
- **Input**: `SendKeys()`
- **ANSI**: `QueueAnsiRequest()`, `GetRequestScheduler()`

### 5 Events
- `SizeChanged` - terminal resized
- `Refreshed` - screen updated
- `ClearedContents` - buffer cleared
- `MouseEvent` - mouse input
- `KeyDown` / `KeyUp` - keyboard input

---

## Step 2: Register the Driver

### Option A: Pass Custom Driver Instance

```csharp
// In Program.cs

var myDriver = new CustomConsoleDriver();
Application.Init(driver: myDriver, driverName: "CustomDriver");

try
{
    using var window = MainWindow.Create(connectionManager, userSettingsManager);
    Application.Run(window);
}
finally
{
    Application.Shutdown();
}
```

### Option B: Use Built-in Driver by Name

```csharp
// Current approach - uses Terminal.Gui's NetDriver
Application.Init(driverName: "NetDriver");

// Alternative built-in drivers:
// - "NetDriver" (uses .NET Console API)
// - "WindowsDriver" (Windows-specific)
// - "CursesDriver" (Unix/Mac with ncurses)
// - "FakeDriver" (unit testing)
```

---

## Step 3: Key Implementation Notes

### Cursor Position Management
- `Move(col, row)` updates **internal** Col/Row properties only
- **Does NOT** move the actual terminal cursor
- `UpdateCursor()` is called separately to move the physical cursor
- Negative or out-of-bounds values still update Col/Row

### Buffer Management
- `Contents` is a 2D array: `Cell[rows, cols]`
- Cell contains character + color attribute
- `Clip` rectangle restricts rendering output
- `AddRune()` and `AddStr()` respect Clip boundaries

### Color Handling
- `CurrentAttribute` = color + style to use for next output
- `Force16Colors` = use 16-color palette instead of TrueColor
- `SupportsTrueColor` = driver capability (read-only)
- `MakeColor()` = convert Color objects to Attribute

### Event Firing
- Fire `SizeChanged` when terminal is resized
- Fire `Refreshed` after `Refresh()` completes
- Fire `KeyDown`/`KeyUp` when keyboard input received
- Fire `MouseEvent` when mouse input received

### ANSI Escape Sequences
- `WriteRaw(ansi)` = output raw escape sequences
- `QueueAnsiRequest()` = queue async ANSI requests
- `GetRequestScheduler()` = access request queue

---

## Step 4: Testing Your Driver

Create a test project to verify driver behavior:

```csharp
[TestFixture]
public class CustomConsoleDriverTests
{
    private CustomConsoleDriver _driver;

    [SetUp]
    public void Setup()
    {
        _driver = new CustomConsoleDriver();
        _driver.Init();
    }

    [TearDown]
    public void TearDown()
    {
        _driver.End();
    }

    [Test]
    public void Init_SetsUpProperties()
    {
        Assert.That(_driver.Cols, Is.GreaterThan(0));
        Assert.That(_driver.Rows, Is.GreaterThan(0));
        Assert.That(_driver.Screen, Is.Not.Null);
    }

    [Test]
    public void Move_UpdatesPosition()
    {
        _driver.Move(5, 10);
        Assert.That(_driver.Col, Is.EqualTo(5));
        Assert.That(_driver.Row, Is.EqualTo(10));
    }

    [Test]
    public void AddRune_IncrementsColumn()
    {
        _driver.Move(0, 0);
        _driver.AddRune(new Rune('A'));
        Assert.That(_driver.Col, Is.EqualTo(1));
    }

    [Test]
    public void Refresh_UpdatesScreen()
    {
        _driver.AddStr("Hello");
        _driver.Refresh(); // Should not throw
    }

    [Test]
    public void GetVersionInfo_Returns()
    {
        var info = _driver.GetVersionInfo();
        Assert.That(info, Does.Contain("CustomConsoleDriver"));
    }
}
```

---

## Step 5: File Structure

```
KustoTerminal.Driver/
├── CustomConsoleDriver.cs          (your implementation)
├── InputHandler.cs                 (keyboard/mouse handling)
├── RenderingEngine.cs              (screen buffer management)
└── ColorManager.cs                 (color quantization)

KustoTerminal.CLI/
└── Program.cs                      (registration)

Tests/
└── CustomConsoleDriverTests.cs     (unit tests)
```

---

## Step 6: Common Implementation Patterns

### Initialize from Console.In/Out

```csharp
public void Init()
{
    Console.OutputEncoding = Encoding.UTF8;
    var size = Console.WindowWidth;  // Get actual terminal size
    Cols = Console.WindowWidth;
    Rows = Console.WindowHeight;
    Screen = new Rectangle(0, 0, Cols, Rows);
    Contents = new Cell[Rows, Cols];
    Clipboard = new SystemClipboard();
    SupportsTrueColor = SupportsColor();
}
```

### Handle Terminal Resize

```csharp
// Listen to Console resize events
Console.CancelKeyPress += (s, e) => { /* handle */ };
// Or use platform-specific APIs (SIGWINCH on Unix, WM_SIZE on Windows)

private void OnTerminalResized(int newCols, int newRows)
{
    Cols = newCols;
    Rows = newRows;
    Screen = new Rectangle(0, 0, Cols, Rows);
    Contents = new Cell[Rows, Cols];
    SizeChanged?.Invoke(this, new SizeChangedEventArgs(Screen));
}
```

### Implement Buffer Rendering

```csharp
public void Refresh()
{
    for (int row = 0; row < Rows; row++)
    {
        for (int col = 0; col < Cols; col++)
        {
            if (Contents[row, col] is Cell cell)
            {
                Console.SetCursorPosition(col, row);
                ApplyAttribute(cell.Attribute);
                Console.Write(cell.Rune);
            }
        }
    }
    UpdateCursor();
    Refreshed?.Invoke(this, EventArgs.Empty);
}
```

---

## Troubleshooting

### Driver Not Recognized
- Ensure driver is registered via `Application.Init(driver: instance)`
- Check that class implements `IConsoleDriver` correctly
- Verify `Init()` is called before rendering

### Rendering Issues
- Check `Contents` is properly sized (Rows × Cols)
- Verify `Clip` rectangle doesn't exclude entire screen
- Test `IsValidLocation()` returns correct values

### Input Not Working
- Ensure `SendKeys()` fires KeyDown/KeyUp events
- Check event handlers are subscribed before `Application.Run()`
- Verify keyboard/mouse handlers call appropriate event methods

### Color Problems
- Check `SupportsTrueColor` matches actual terminal capability
- Test `Force16Colors` reduces colors correctly
- Verify `MakeColor()` quantizes colors appropriately

---

## References

- **Interface Definition**: `/Users/basselsaba/.nuget/packages/terminal.gui/2.0.0-alpha.3721/lib/net8.0/Terminal.Gui.xml`
- **Template**: `CustomConsoleDriver_Template.cs`
- **Terminal.Gui GitHub**: https://github.com/migueldeicaza/gui.cs
- **Current Usage**: `KustoTerminal.CLI/Program.cs` line 44

---

## Example: Minimal Implementation

```csharp
public class MinimalDriver : IConsoleDriver
{
    public IClipboard Clipboard { get; } = new SystemClipboard();
    public Rectangle Screen { get; set; } = new(0, 0, 80, 24);
    public Rectangle Clip { get; set; }
    public int Col { get; set; }
    public int Cols => 80;
    public Cell[,] Contents { get; set; } = new Cell[24, 80];
    public int Left => 0;
    public int Row { get; set; }
    public int Rows => 24;
    public int Top => 0;
    public bool SupportsTrueColor { get; } = true;
    public bool Force16Colors { get; set; }
    public Attribute CurrentAttribute { get; set; }

    public event EventHandler<SizeChangedEventArgs> SizeChanged;
    public event EventHandler Refreshed;
    public event EventHandler ClearedContents;
    public event EventHandler<MouseEventArgs> MouseEvent;
    public event EventHandler<KeyEventArgs> KeyDown;
    public event EventHandler<KeyEventArgs> KeyUp;

    public void Init() { Console.Clear(); }
    public void End() { }
    public void Suspend() { }
    public void Move(int col, int row) { Col = col; Row = row; }
    public void UpdateCursor() { }
    public void AddRune(Rune rune) { Col++; }
    public void AddRune(char c) => AddRune(new Rune(c));
    public void AddStr(string str) { Col += str.Length; }
    public void FillRect(Rectangle rect, Rune rune) { }
    public void FillRect(Rectangle rect, char c) { }
    public void ClearContents() { }
    public void Refresh() { Refreshed?.Invoke(this, EventArgs.Empty); }
    public void UpdateScreen() => Refresh();
    public void SetAttribute(Attribute attr) { CurrentAttribute = attr; }
    public Attribute GetAttribute() => CurrentAttribute;
    public Attribute MakeColor(ref Color f, ref Color b) => CurrentAttribute;
    public bool GetCursorVisibility(out CursorVisibility v) { v = CursorVisibility.Default; return true; }
    public bool SetCursorVisibility(CursorVisibility v) => true;
    public bool IsRuneSupported(Rune rune) => true;
    public bool IsValidLocation(Rune rune, int col, int row) => true;
    public string GetVersionInfo() => "MinimalDriver";
    public void WriteRaw(string ansi) { }
    public void SendKeys(char c, ConsoleKey key, bool shift, bool alt, bool ctrl) { }
    public void QueueAnsiRequest(AnsiEscapeSequenceRequest req) { }
    public AnsiRequestScheduler GetRequestScheduler() => null;
}
```

---

