# Terminal.Gui Diagnostics Logging

This document explains how diagnostics logging has been enabled for Terminal.Gui in the Kusto Terminal application.

## Features Enabled

### 1. System Console Integration
- `Application.UseSystemConsole = true` enables Terminal.Gui to use the system console for debug output
- This allows Terminal.Gui internal messages to be captured and logged

### 2. File Logging
- Diagnostic logs are written to timestamped files in the `logs/` directory
- Log files follow the pattern: `terminal_gui_diagnostics_YYYYMMDD_HHMMSS.log`
- The log file path is displayed during application startup

### 3. Environment Variables
- `DEBUG_TUI=1` - Enables Terminal.Gui debug mode
- `TUI_DEBUG=1` - Additional debug flag for Terminal.Gui internals

### 4. Trace Listeners
- Console trace listener for immediate console output
- File trace listener for persistent logging
- Auto-flush enabled for immediate log writing

### 5. Event Logging
- Application initialization events
- Terminal.Gui configuration details (driver type, screen size)
- Keyboard event logging for debugging input issues

## Log Information Captured

The diagnostics system captures:
- Application startup and initialization
- Terminal.Gui driver information
- Screen dimensions and configuration
- Keyboard input events
- Application lifecycle events
- Any Terminal.Gui internal debug messages

## Usage

When you run the application, you'll see a message like:
```
Terminal.Gui diagnostics enabled. Logs will be written to: /path/to/logs/terminal_gui_diagnostics_20250908_101234.log
```

The logs directory structure:
```
logs/
├── terminal_gui_diagnostics_20250908_101234.log
├── terminal_gui_diagnostics_20250908_102345.log
└── ...
```

## Log File Example

```
Terminal.Gui diagnostics started at 2025-09-08 10:12:34
Terminal.Gui Configuration:
  UseSystemConsole: True
  Driver: CursesDriver
  Screen Size: 120x30
Terminal.Gui keyboard event logging enabled
Terminal.Gui debug configuration completed
Key pressed: F5
Key pressed: Tab
...
```

## Implementation Details

The diagnostics are enabled in [`Program.cs`](../src/KustoTerminal.CLI/Program.cs) through two methods:

1. `EnableTerminalGuiDiagnostics()` - Called before `Application.Init()`
   - Sets up file logging
   - Configures trace listeners
   - Sets environment variables

2. `ConfigureTerminalGuiDebug()` - Called after `Application.Init()`
   - Logs Terminal.Gui configuration
   - Sets up keyboard event logging
   - Logs driver and screen information

## Troubleshooting

If diagnostics are not working:
1. Check that the `logs/` directory is created
2. Verify file permissions for log file creation
3. Look for warning messages during startup
4. Check that `Application.UseSystemConsole` is set to `true`

## Performance Impact

The diagnostics logging has minimal performance impact:
- File I/O is buffered with auto-flush
- Only debug/trace messages are logged
- Keyboard events are logged efficiently
- No impact on normal application functionality