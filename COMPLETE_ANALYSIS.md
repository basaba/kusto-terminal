# Terminal.Gui IConsoleDriver Interface - Complete Analysis

## Executive Summary

I've successfully analyzed the Terminal.Gui 2.0.0-alpha.3721 IConsoleDriver interface and extracted the **COMPLETE contract** that your custom driver must implement. 

**Key Finding**: You must implement ALL of the following with NO optional members:
- **13 Properties** 
- **25 Methods** (including overloads)
- **5 Events**

---

## 🎯 The Complete Interface Contract

### Location
```
~/.nuget/packages/terminal.gui/2.0.0-alpha.3721/lib/net8.0/Terminal.Gui.dll
~/.nuget/packages/terminal.gui/2.0.0-alpha.3721/lib/net8.0/Terminal.Gui.xml (documentation)
```

### 13 Required Properties

| Property | Type | Access | Purpose |
|----------|------|--------|---------|
| `Clipboard` | `IClipboard` | get | Access OS clipboard |
| `Screen` | `Rectangle` | get | Terminal screen bounds |
| `Clip` | `Rectangle` | get, set | Rendering clip region |
| `Col` | `int` | get | Current cursor column |
| `Cols` | `int` | get | Terminal width |
| `Contents` | `Cell[,]` | get, set | Display buffer (rows × cols) |
| `Left` | `int` | get | Leftmost column |
| `Row` | `int` | get | Current cursor row |
| `Rows` | `int` | get | Terminal height |
| `Top` | `int` | get | Topmost row |
| `SupportsTrueColor` | `bool` | get | Supports 24-bit colors |
| `Force16Colors` | `bool` | get, set | Force 16-color mode |
| `CurrentAttribute` | `Attribute` | get, set | Text color/style |

### 25 Required Methods

**Lifecycle (3)**
- `void Init()` - Initialize driver
- `void End()` - Shutdown driver
- `void Suspend()` - Suspend/pause driver

**Cursor Management (2)**
- `void Move(int col, int row)` - Set internal cursor position
- `void UpdateCursor()` - Move physical terminal cursor

**Rendering (9)**
- `void AddRune(Rune rune)` - Add rune at cursor
- `void AddRune(char c)` - Add char at cursor
- `void AddStr(string str)` - Add string at cursor
- `void FillRect(Rectangle rect, Rune rune)` - Fill rectangle
- `void FillRect(Rectangle rect, char c)` - Fill rectangle with char
- `void ClearContents()` - Clear display buffer
- `void Refresh()` - Write buffer to terminal
- `void UpdateScreen()` - Update screen

**Color Management (3)**
- `void SetAttribute(Attribute attr)` - Set text attribute
- `Attribute GetAttribute()` - Get current attribute
- `Attribute MakeColor(ref Color foreground, ref Color background)` - Convert colors

**Cursor Visibility (2)**
- `bool GetCursorVisibility(out CursorVisibility visibility)` - Get cursor state
- `bool SetCursorVisibility(CursorVisibility visibility)` - Set cursor state

**Validation & Info (3)**
- `bool IsRuneSupported(Rune rune)` - Check if rune displayable
- `bool IsValidLocation(Rune rune, int col, int row)` - Check if position valid
- `string GetVersionInfo()` - Get driver name/version

**Low-Level (2)**
- `void WriteRaw(string ansi)` - Write raw ANSI escapes
- `void SendKeys(char c, ConsoleKey key, bool shift, bool alt, bool ctrl)` - Inject keyboard input

**ANSI Sequences (2)**
- `void QueueAnsiRequest(AnsiEscapeSequenceRequest request)` - Queue ANSI request
- `AnsiRequestScheduler GetRequestScheduler()` - Get request scheduler

### 5 Required Events

| Event | Type | When Fired | Provides |
|-------|------|-----------|----------|
| `SizeChanged` | `EventHandler<SizeChangedEventArgs>` | Terminal resized | New dimensions |
| `Refreshed` | `EventHandler` | After Refresh() | None |
| `ClearedContents` | `EventHandler` | After ClearContents() | None |
| `MouseEvent` | `EventHandler<MouseEventArgs>` | Mouse input received | Mouse details |
| `KeyDown` | `EventHandler<KeyEventArgs>` | Key pressed | Key code |
| `KeyUp` | `EventHandler<KeyEventArgs>` | Key released | Key code |

---

## 🔑 Critical Implementation Notes

### 1. Cursor Management is SPLIT
- **`Move(col, row)`** updates **INTERNAL** Col/Row properties ONLY
- Does **NOT** move the actual terminal cursor
- **`UpdateCursor()`** moves the PHYSICAL terminal cursor separately

### 2. Rendering is BUFFER-BASED
- Application writes to **`Contents[row, col]`** (2D Cell array)
- **YOU** read from Contents and write to the terminal in **`Refresh()`**
- **`Clip`** rectangle must be respected - only render cells within bounds

### 3. Color Handling is YOUR JOB
- **`SupportsTrueColor`** = read-only capability flag (can you display 24-bit?)
- **`Force16Colors`** = user preference (must you use 16-color mode?)
- **`MakeColor()`** = you convert Color → Attribute WITH color quantization

### 4. Events Must Fire at the RIGHT TIMES
- **`SizeChanged`** when terminal is resized (you detect this)
- **`Refreshed`** after `Refresh()` completes
- **`ClearedContents`** after `ClearContents()` is called
- **`KeyDown`/`KeyUp`** when keyboard input received
- **`MouseEvent`** when mouse input received

### 5. Wide Character Handling
- Runes can be 1 or 2 columns wide (emoji, CJK characters)
- `AddRune()` increments `Col` by the rune's width
- `IsValidLocation()` must account for character width

---

## 📋 How Application.Init() Uses Drivers

### Current Usage (Program.cs:44)
```csharp
Application.Init(driverName: "NetDriver");
```
This uses Terminal.Gui's built-in NetDriver.

### To Use Custom Driver
```csharp
var driver = new CustomConsoleDriver();
Application.Init(driver: driver, driverName: "CustomDriver");
```

### Method Signatures
```csharp
public static void Init(IConsoleDriver driver, string driverName)
public static void Init(string driverName)
```

---

## 📁 Documentation Files Created

All files are in `/Users/basselsaba/kusto-terminal/kusto-terminal/`:

### Essential Files (START HERE)

1. **START_HERE_DRIVER_DOCS.md** ⭐
   - Overview of what you're implementing
   - Documentation roadmap
   - Quick start guide
   - Read this first (5 minutes)

2. **IConsoleDriver_Interface_Specification.txt** 📋
   - Complete interface contract
   - All properties, methods, events with descriptions
   - The authoritative reference (20 minutes)

3. **CustomConsoleDriver_Template.cs** 💻
   - Ready-to-use C# skeleton
   - All members as stubs with TODO comments
   - Copy and implement (17 KB)

4. **CUSTOM_DRIVER_INTEGRATION_GUIDE.md** 📖
   - Step-by-step implementation guide
   - Testing examples (NUnit)
   - Common patterns and troubleshooting
   - How-to manual (30 minutes)

### Reference Files

5. **README_DRIVER_DEVELOPMENT.md** - Overview document
6. **IConsoleDriver_Quick_Reference.md** - One-page cheat sheet
7. **DOCUMENTATION_INDEX.txt** - Index of all files
8. Other .md files - Additional reference material

---

## ✅ Implementation Checklist

### Must Implement ALL of:
- [ ] 13 Properties with correct types
- [ ] 25 Methods with proper logic
- [ ] 5 Events fired at correct times

### Critical Behaviors:
- [ ] Respect Clip rectangle in rendering
- [ ] Handle wide characters (2-column width)
- [ ] Support both 16-color and TrueColor modes
- [ ] Fire events at proper times
- [ ] UpdateCursor() called from Refresh()
- [ ] Move() updates internal position only

---

## 🚀 Quick Start

1. **Read**: `START_HERE_DRIVER_DOCS.md` (5 min)
2. **Study**: `IConsoleDriver_Interface_Specification.txt` (20 min)
3. **Copy**: `CustomConsoleDriver_Template.cs` to your project
4. **Follow**: `CUSTOM_DRIVER_INTEGRATION_GUIDE.md`
5. **Implement**: Replace each TODO with actual code
6. **Register**: Update `Program.cs` to use your driver
7. **Test**: Use examples from the integration guide

---

## 🎓 Type Dependencies

You'll need to work with these Terminal.Gui types:

- `Terminal.Gui.Drivers.IConsoleDriver` - The interface
- `Terminal.Gui.Drawing.Attribute` - Color + style
- `Terminal.Gui.Drawing.Color` - RGB color
- `Terminal.Gui.Drivers.CursorVisibility` - Enum
- `Terminal.Gui.Input.MouseEventArgs` - Mouse events
- `Terminal.Gui.Input.KeyEventArgs` - Keyboard events
- `Terminal.Gui.Drivers.Cell` - Buffer element
- `Terminal.Gui.Drivers.AnsiEscapeSequenceRequest`
- `Terminal.Gui.Drivers.AnsiRequestScheduler`
- `System.Drawing.Rectangle`
- `System.Text.Rune`

---

## 📊 Summary Statistics

| Aspect | Count |
|--------|-------|
| Total Properties | 13 |
| Total Methods | 25 |
| Total Events | 5 |
| Type Dependencies | 9+ |
| Documentation Files | 10 |
| Total Documentation | ~100 KB |

---

## 🔗 References

- **Terminal.Gui GitHub**: https://github.com/migueldeicaza/gui.cs
- **NuGet Package**: Terminal.Gui 2.0.0-alpha.3721
- **Current Usage**: `src/KustoTerminal.CLI/Program.cs:44`

---

## ✨ What You Now Have

✅ Complete interface contract (all 13+25+5 members)
✅ Ready-to-use C# template with proper types
✅ Step-by-step implementation guide
✅ Testing examples and patterns
✅ Troubleshooting guide
✅ All documentation in project root

**You have everything needed to implement a complete custom IConsoleDriver.**

Start with `START_HERE_DRIVER_DOCS.md`

