# Terminal.Gui IConsoleDriver Interface - Complete Reference

This directory contains comprehensive documentation for implementing a custom Terminal.Gui console driver for the KustoTerminal application.

## 📋 Documentation Files

### 1. **IConsoleDriver_Interface_Specification.txt**
The complete, authoritative interface contract extracted from Terminal.Gui 2.0.0-alpha.3721.

Contains:
- All 13 properties with descriptions
- All 25 methods with detailed explanations
- All 5 events with trigger conditions
- Type dependencies
- Reference implementations

**Usage**: Reference this when implementing any method/property.

### 2. **CustomConsoleDriver_Template.cs**
A ready-to-use C# template with complete method signatures and TODO comments.

Contains:
- All 13 properties with proper types
- All 25 methods with explanatory comments
- All 5 events with declarations
- Helper methods for event firing
- Proper grouping and documentation

**Usage**: Copy this as your starting point, then implement each method.

### 3. **CUSTOM_DRIVER_INTEGRATION_GUIDE.md**
Step-by-step guide for creating and integrating a custom driver.

Contains:
- Implementation checklist
- Registration instructions (2 options)
- Critical implementation notes
- Testing examples (NUnit)
- Common patterns and examples
- Troubleshooting guide
- Minimal reference implementation

**Usage**: Follow this guide when building your driver.

## 🎯 Quick Start

### 1. Understand the Interface
```bash
cat IConsoleDriver_Interface_Specification.txt
```

### 2. Copy the Template
```bash
cp CustomConsoleDriver_Template.cs src/KustoTerminal.Driver/CustomConsoleDriver.cs
```

### 3. Implement Each Method
- Read the specification for each method
- Replace `throw new NotImplementedException()` with your code
- Fire events at appropriate times

### 4. Register in Program.cs
```csharp
var driver = new CustomConsoleDriver();
Application.Init(driver: driver, driverName: "CustomDriver");
```

### 5. Test
Use the testing examples in CUSTOM_DRIVER_INTEGRATION_GUIDE.md.

## 🔍 Key Insights

### The Interface is NOT Abstract
- `IConsoleDriver` is an **interface**, not an abstract base class
- You implement it directly, no inheritance needed
- You must provide ALL 13 properties and ALL 25 methods

### Cursor Management is Split
- `Move(col, row)` = Update **internal** position only
- `UpdateCursor()` = Move **actual** terminal cursor separately

### Rendering is Buffer-Based
- Application writes to `Contents` buffer
- You read from `Contents` and write to terminal in `Refresh()`
- `Clip` rectangle restricts which cells are rendered

### Color Quantization is Your Job
- `SupportsTrueColor` = can you display 24-bit colors?
- `Force16Colors` = user preference to use 16 colors instead
- `MakeColor()` = you convert Color → Attribute, with quantization if needed

### Events Must Fire at Right Times
- `SizeChanged` when terminal resized
- `Refreshed` after `Refresh()` completes
- `KeyDown`/`KeyUp` when receiving keyboard input
- `MouseEvent` when receiving mouse input
- `ClearedContents` when `ClearContents()` is called

## 📊 Statistics

| Aspect | Count |
|--------|-------|
| Properties | 13 |
| Methods | 25 |
| Events | 5 |
| Type Dependencies | 9+ |
| Lines in Specification | 300+ |
| Lines in Template | 500+ |
| Guide Sections | 7 |

## 🔗 File Locations

**Terminal.Gui NuGet Package:**
```
~/.nuget/packages/terminal.gui/2.0.0-alpha.3721/
├── lib/net8.0/Terminal.Gui.dll          (compiled assembly)
├── lib/net8.0/Terminal.Gui.xml          (documentation)
└── terminal.gui.2.0.0-alpha.3721.nupkg  (package file)
```

**Current Usage in KustoTerminal:**
```
src/KustoTerminal.CLI/Program.cs:44
    Application.Init(driverName: "NetDriver");
```

## 🚀 Next Steps

1. Read `IConsoleDriver_Interface_Specification.txt` in full
2. Review `CUSTOM_DRIVER_INTEGRATION_GUIDE.md` step-by-step
3. Copy `CustomConsoleDriver_Template.cs` to your project
4. Implement methods one by one
5. Write unit tests (examples provided in guide)
6. Register and test with your application

## 📚 Additional Resources

- **Terminal.Gui Repository**: https://github.com/migueldeicaza/gui.cs
- **Issue Tracker**: https://github.com/migueldeicaza/gui.cs/issues
- **Discussions**: https://github.com/migueldeicaza/gui.cs/discussions

## ⚠️ Important Notes

- Terminal.Gui 2.0.0-alpha.3721 is an ALPHA release
- API may change between versions
- Test thoroughly on target platforms (Windows, macOS, Linux)
- The built-in `NetDriver` is a solid reference implementation
- Consider starting with a thin wrapper around existing code

## 📝 Created Files Summary

```
IConsoleDriver_Interface_Specification.txt   (Authority - use this!)
CustomConsoleDriver_Template.cs              (Template - copy this!)
CUSTOM_DRIVER_INTEGRATION_GUIDE.md           (Guide - follow this!)
README_DRIVER_DEVELOPMENT.md                 (This file)
```

All files are in the project root directory for easy reference.

---

**Last Updated**: 2025-01-XX  
**Source**: Terminal.Gui 2.0.0-alpha.3721  
**Package**: `/Users/basselsaba/.nuget/packages/terminal.gui/2.0.0-alpha.3721/`
