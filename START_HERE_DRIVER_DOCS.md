# 🚀 START HERE - Terminal.Gui IConsoleDriver Documentation

## What You Need to Know

You're implementing a **custom Terminal.Gui console driver** for KustoTerminal.

Terminal.Gui 2.0.0-alpha.3721 requires you to implement the `IConsoleDriver` interface, which defines:
- **13 Properties** (terminal state, colors, cursor position)
- **25 Methods** (rendering, input, lifecycle)
- **5 Events** (resize, refresh, keyboard, mouse)

## 📚 Documentation Files (Read in This Order)

### 1️⃣ **START WITH THIS** → `IConsoleDriver_Interface_Specification.txt`
- **What**: Complete interface contract extracted from Terminal.Gui XML docs
- **Contains**: All 13 properties, 25 methods, 5 events with full descriptions
- **Size**: 14 KB
- **Time**: 15-20 minutes to read
- **Action**: Read this to understand what you MUST implement

### 2️⃣ **THEN USE THIS** → `CustomConsoleDriver_Template.cs`
- **What**: Ready-to-compile C# skeleton with all members
- **Contains**: All properties, methods, events as stubs with TODO comments
- **Size**: 17 KB (500+ lines)
- **Time**: 5 minutes to review
- **Action**: Copy this file to your project and replace `throw new NotImplementedException()` calls

### 3️⃣ **FOLLOW THIS** → `CUSTOM_DRIVER_INTEGRATION_GUIDE.md`
- **What**: Step-by-step implementation guide with examples
- **Contains**: Registration methods, testing examples, common patterns, troubleshooting
- **Size**: 10 KB
- **Time**: 20-30 minutes to follow
- **Action**: Use this when actually implementing your driver

### 4️⃣ **REFERENCE THESE** → Other Documentation Files
- `README_DRIVER_DEVELOPMENT.md` - Overview and structure
- `IConsoleDriver_Quick_Reference.md` - One-page cheat sheet
- `README_IConsoleDriver.md` - Additional details
- Others - Supplementary information

## 🎯 Quick Summary

### What is IConsoleDriver?
It's an **interface** (not abstract class) that you must fully implement. Think of it as a contract:
- **The application** writes to `Contents` buffer
- **You** read from `Contents` and display it on the terminal
- **The application** calls your methods to render, handle input, etc.

### Key Insights
1. **Split Cursor Management**
   - `Move(col, row)` = internal position only
   - `UpdateCursor()` = actual terminal cursor movement

2. **Buffer-Based Rendering**
   - `Contents[row, col]` = what to display
   - `Refresh()` = write buffer to terminal
   - `Clip` rectangle = rendering boundary

3. **You Control Colors**
   - Convert `Color` → `Attribute` in `MakeColor()`
   - Handle 16-color vs TrueColor modes
   - Respect `Force16Colors` setting

4. **Events Matter**
   - Fire at the right times (SizeChanged, Refreshed, Key events, etc.)
   - Application subscribes to these to react to input/resize

### Example: How It Works

```csharp
// Application does this:
driver.Move(0, 0);           // Set internal cursor position
driver.AddStr("Hello");       // Add "Hello" to buffer at cursor
driver.Refresh();             // YOUR job: write buffer to terminal

// Your Refresh() implementation does:
public void Refresh()
{
    // Read Contents buffer
    for (int row = 0; row < Rows; row++)
    {
        for (int col = 0; col < Cols; col++)
        {
            var cell = Contents[row, col];
            Console.SetCursorPosition(col, row);
            ApplyAttribute(cell.Attribute);  // Set color
            Console.Write(cell.Rune);        // Write character
        }
    }
    UpdateCursor();           // Move actual cursor
    Refreshed?.Invoke(...);   // Fire event
}
```

## 🛠 Implementation Roadmap

```
Step 1: Understand the Interface (30 min)
        └─ Read IConsoleDriver_Interface_Specification.txt

Step 2: Set up Project Structure (10 min)
        └─ Create src/KustoTerminal.Driver/ folder
        └─ Copy CustomConsoleDriver_Template.cs there

Step 3: Implement Core Methods (4-6 hours)
        ├─ Init() / End() / Suspend()           [Lifecycle]
        ├─ Move() / UpdateCursor()              [Positioning]
        ├─ AddRune() / AddStr() / FillRect()    [Rendering]
        ├─ Refresh() / UpdateScreen()           [Display update]
        ├─ SetAttribute() / GetAttribute()      [Color management]
        └─ Other methods...

Step 4: Implement Input Handling (1-2 hours)
        ├─ SendKeys()
        └─ Fire KeyDown/KeyUp/MouseEvent events

Step 5: Register in Program.cs (5 min)
        └─ var driver = new CustomConsoleDriver();
        └─ Application.Init(driver: driver, driverName: "CustomDriver");

Step 6: Test (varies)
        └─ Use examples in CUSTOM_DRIVER_INTEGRATION_GUIDE.md
        └─ Verify all events fire correctly
        └─ Test rendering, colors, input
```

## 🔗 Current Driver Usage

**File**: `src/KustoTerminal.CLI/Program.cs`
**Line**: 44
**Current Code**:
```csharp
Application.Init(driverName: "NetDriver");
```

**To Use Custom Driver**:
```csharp
var driver = new CustomConsoleDriver();
Application.Init(driver: driver, driverName: "CustomDriver");
```

## 📊 Interface at a Glance

| Category | Count | Notes |
|----------|-------|-------|
| Properties | 13 | All must be implemented |
| Methods | 25 | Includes overloads |
| Events | 5 | Must fire at proper times |
| Types to Know | 9+ | Attribute, Color, Cell, etc. |

## ⚙️ NuGet Package Location

```
~/.nuget/packages/terminal.gui/2.0.0-alpha.3721/
├── lib/net8.0/Terminal.Gui.dll       (compiled assembly)
└── lib/net8.0/Terminal.Gui.xml       (documentation)
```

This is where the interface definitions come from.

## ✅ Checklist Before Starting

- [ ] Read `IConsoleDriver_Interface_Specification.txt`
- [ ] Review `CustomConsoleDriver_Template.cs`
- [ ] Understand the 5 events and when they fire
- [ ] Know that Move() ≠ UpdateCursor()
- [ ] Know that Contents is a 2D array [rows, cols]
- [ ] Know you must handle wide characters (emoji, CJK)
- [ ] Know you must respect the Clip rectangle
- [ ] Know you must handle colors with quantization

## 🆘 If You Get Stuck

1. **Method not clear?** → Check specification for detailed description
2. **How to implement?** → See common patterns in integration guide
3. **What does X mean?** → Check type definitions or XML docs
4. **Runtime error?** → See troubleshooting in integration guide
5. **Need example?** → See minimal reference implementation in guide

## 📞 Quick Reference

**Terminal.Gui Namespace**: `Terminal.Gui.Drivers`

**Interface**: `IConsoleDriver`

**Registration**: 
```csharp
Application.Init(driver: instance, driverName: "YourName");
```

**Built-in Drivers**:
- NetDriver (current - uses .NET Console API)
- WindowsDriver
- CursesDriver
- FakeDriver (testing)

## 🎓 Key Files Summary

| File | Purpose | Size | Audience |
|------|---------|------|----------|
| `IConsoleDriver_Interface_Specification.txt` | Authoritative spec | 14 KB | Everyone |
| `CustomConsoleDriver_Template.cs` | Code skeleton | 17 KB | Implementers |
| `CUSTOM_DRIVER_INTEGRATION_GUIDE.md` | Step-by-step guide | 10 KB | Implementers |
| `README_DRIVER_DEVELOPMENT.md` | Overview | 8 KB | Everyone |
| Other .md files | Reference info | 4-9 KB | As needed |

## 🚀 Next Step

👉 **Open and read**: `IConsoleDriver_Interface_Specification.txt`

This is your source of truth for what must be implemented.

---

**Generated from**: Terminal.Gui 2.0.0-alpha.3721
**Package Location**: `~/.nuget/packages/terminal.gui/2.0.0-alpha.3721/`
**Target**: Custom IConsoleDriver implementation for KustoTerminal
