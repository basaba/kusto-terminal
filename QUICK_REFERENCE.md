# Kusto Terminal Architecture - Quick Reference

## 1. View/Window Classes Summary

| Class | File | Extends | Purpose |
|-------|------|---------|---------|
| **MainWindow** | `UI/MainWindow.cs` | `Window` | Root container; assembles 3 panes in split layout |
| **ConnectionPane** | `UI/Panes/ConnectionPane.cs` | `View` | Left sidebar: cluster/database tree + connection mgmt |
| **QueryEditorPane** | `UI/Panes/QueryEditorPane.cs` | `BasePane` | Top-right: query editor with syntax coloring, autocomplete |
| **ResultsPane** | `UI/Panes/ResultsPane.cs` | `BasePane` | Bottom-right: table view, time charts, search, filtering |
| **TimeChartView** | `UI/Charts/TimeChartView.cs` | `View` | Renders time-series using Braille Unicode |
| **SafeTextView** | `UI/Controls/SafeTextView.cs` | `TextView` | TextView wrapper with mouse event bug fixes |
| **LineNumberGutterView** | `UI/Controls/LineNumberGutterView.cs` | `View` | Line number gutter synced with editor scroll |
| **ConnectionDialog** | `UI/Dialogs/ConnectionDialog.cs` | `Dialog` | Add/edit connection (cluster URI, database, auth) |
| **ColumnSelectorDialog** | `UI/Dialogs/ColumnSelectorDialog.cs` | `Dialog` | Select columns to display in results |
| **JsonTreeViewDialog** | `UI/Dialogs/JsonTreeViewDialog.cs` | `Dialog` | Expand/explore JSON cells in results |
| **ShareDialog** | `UI/Dialogs/ShareDialog.cs` | `Dialog` | Copy query and/or results to clipboard |
| **ClusterTreeNode** | `UI/Models/ClusterTreeNode.cs` | `TreeNode` | Tree node for cluster in connection pane |
| **DatabaseTreeNode** | `UI/Models/ClusterTreeNode.cs` | `TreeNode` | Tree node for database under cluster |

## 2. Screen Composition Entry Points

**Main Entry:** `src/KustoTerminal.CLI/Program.cs`
```
1. Initialize ConnectionManager, UserSettingsManager
2. Load existing connections from JSON
3. Application.Init() with KustoConsoleDriver (or fallback to NetDriver)
4. MainWindow.Create(connectionManager, userSettingsManager)
5. Application.Run(window)
```

**Screen Assembly:** `src/KustoTerminal.UI/MainWindow.cs` (line 85-209)
- Creates 3 FrameView containers
- Creates 3 Panes (Connection, QueryEditor, Results)
- Subscribes to pane events
- Sets up keyboard shortcuts (Ctrl+Q, F12, Alt+Cursor)

## 3. Event/Messaging System

**Type:** Direct event-driven (NO centralized event bus)

**Primary Events:**
```csharp
ConnectionPane:
  event EventHandler<KustoConnection> ConnectionSelected

QueryEditorPane:
  event EventHandler<string> QueryExecuteRequested      // Carries query text
  event EventHandler QueryCancelRequested
  event EventHandler MaximizeToggleRequested

ResultsPane:
  event EventHandler MaximizeToggleRequested

IConnectionManager:
  event EventHandler<KustoConnection> ConnectionAddOrUpdated
```

**Example Flow:** Query Execution
1. User presses Ctrl+Enter in QueryEditorPane
2. `QueryExecuteRequested` event fires
3. `MainWindow.OnQueryExecuteRequested()` catches it
4. Calls `ExecuteQueryAsync()` → creates KustoClient → executes query
5. On completion, calls `ResultsPane.DisplayResult(result)` via `Application.Invoke()`

## 4. Tab System

**Status: DOES NOT EXIST**

Current: Single query editor, single results pane (last result overwrites previous)

To implement tabs:
- Add TabView/TabBar above query editor
- Store `List<QueryTab>` with editor + result per tab
- Track `activeTabIndex`
- Route events from tab panes to MainWindow

## 5. Connection Model

**Definition:** `src/KustoTerminal.Core/Models/KustoConnection.cs`
```csharp
public class KustoConnection
{
    public string Id { get; set; }                    // GUID
    public string Name { get; set; }                  // User-friendly name
    public string ClusterUri { get; set; }            // https://cluster.kusto.windows.net
    public string Database { get; set; }              // Database name
    public List<string> Databases { get; set; }       // Cached list of DBs
    public AuthenticationType AuthType { get; set; }  // AzureCli (only option)
}
```

**Persistence:** `src/KustoTerminal.Core/Services/ConnectionManager.cs`
- File: `%APPDATA%\KustoTerminal\connections.json`
- Interface: `IConnectionManager`
- Methods: `AddConnectionAsync()`, `UpdateConnectionAsync()`, `DeleteConnectionAsync()`, `GetConnectionsAsync()`
- Event: `ConnectionAddOrUpdated` → MainWindow subscribes to preload cluster schemas

## 6. Query Execution Flow

**Complete Trace:**

```
User Input (QueryEditorPane)
    ↓
QueryEditorPane.QueryExecuteRequested event
    ↓
MainWindow.OnQueryExecuteRequested() [line 287-289]
    ↓
MainWindow.ExecuteQueryAsync(query) [line 453-540]
    │
    ├→ ConnectionPane.GetSelectedConnection()
    ├→ AuthenticationProviderFactory.CreateProvider(authType)
    ├→ new KustoClient(connection, authProvider)
    │
    └→ KustoClient.ExecuteQueryAsync() [Core/Services/KustoClient.cs line 32]
        │
        ├→ EnsureConnectionAsync() [Initialize ICslQueryProvider/ICslAdminProvider]
        ├→ Prepare ClientRequestProperties with unique request ID
        ├→ Check if query is command (starts with ".")
        ├→ ExecuteQueryAsync() or ExecuteControlCommandAsync() via Kusto.Data SDK
        ├→ DataTable.Load(reader)
        ├→ TimeChartDetector.IsTimeChartData(dataTable)
        │
        └→ Return QueryResult { DataTable, IsTimeChart, ... }
    
    └→ Application.Invoke() [Update UI thread]
        ├→ QueryEditorPane.SetExecuting(false)
        ├→ ResultsPane.SetQueryText(query)
        ├→ ResultsPane.SetConnection(connection)
        │
        └→ ResultsPane.DisplayResult(result)
            ├→ TableView.Update(result.DataTable)
            ├→ If IsTimeChart: show TimeChartView
            └→ Update status label (rows, columns)
```

**Cancellation:**
- User presses Ctrl+C
- `QueryCancelRequested` event fires
- MainWindow calls `_queryCancellationTokenSource.Cancel()`
- KustoClient detects via CancellationToken, throws `OperationCanceledException`
- UI updates: `QueryEditorPane.SetExecuting(false)`

## Supporting Services

| Service | File | Purpose |
|---------|------|---------|
| **KustoClient** | `Core/Services/KustoClient.cs` | Execute queries via Kusto.Data SDK |
| **ConnectionManager** | `Core/Services/ConnectionManager.cs` | Load/save connections JSON |
| **ClusterSchemaService** | `Core/Services/ClusterSchemaService.cs` | Fetch schemas for autocomplete |
| **TimeChartDetector** | `Core/Services/TimeChartDetector.cs` | Detect time-series result patterns |
| **SyntaxHighlighter** | `UI/SyntaxHighlighting/SyntaxHighlighter.cs` | KQL syntax coloring |
| **AutocompleteSuggestionGenerator** | `UI/AutoCompletion/AutocompleteSuggestionGenerator.cs` | Autocomplete suggestions |
| **LanguageService** | `Language/Services/LanguageService.cs` | KQL parsing & classification |

## Key Files

| File | Lines | Purpose |
|------|-------|---------|
| `Program.cs` | 100 | Application entry point, driver init |
| `MainWindow.cs` | 621 | Screen layout, event routing, query execution |
| `QueryEditorPane.cs` | 300+ | Editor UI, syntax highlighting, execution trigger |
| `ResultsPane.cs` | 400+ | Results display, table view, charts, search |
| `ConnectionPane.cs` | 300+ | Connection tree, selection |
| `KustoClient.cs` | 200+ | Kusto query execution |
| `ConnectionManager.cs` | 100+ | Connection persistence |
| `SyntaxHighlighter.cs` | 100+ | Query syntax coloring |

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+Q`, `Ctrl+C` | Quit |
| `Ctrl+N` (in connections) | New connection |
| `Ctrl+E` (in connections) | Edit connection |
| `Del` (in connections) | Delete connection |
| `Space` (in connections) | Refresh |
| `Ctrl+Enter` (in editor) | Execute query |
| `Ctrl+C` (in editor) | Cancel query |
| `F12` | Maximize/restore current pane |
| `Alt+→` / `Alt+←` | Switch between left/right panes |
| `Alt+↓` / `Alt+↑` | Switch between editor/results |

