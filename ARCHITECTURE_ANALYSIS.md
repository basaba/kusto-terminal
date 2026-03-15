# Kusto Terminal Architecture Analysis

## 1. ALL VIEW/WINDOW CLASSES

### Main Window
- **MainWindow** (`src/KustoTerminal.UI/MainWindow.cs`)
  - Extends: `Window`
  - Purpose: Root application container that assembles all panes (Connection, Query Editor, Results) into a 3-panel layout with maximize/minimize support

### Panes (Sub-Views)
- **ConnectionPane** (`src/KustoTerminal.UI/Panes/ConnectionPane.cs`)
  - Extends: `View`
  - Purpose: Left sidebar showing connection/cluster/database tree; manages connection selection and lifecycle

- **QueryEditorPane** (`src/KustoTerminal.UI/Panes/QueryEditorPane.cs`)
  - Extends: `BasePane`
  - Purpose: Top-right panel with query text editor, syntax highlighting, autocomplete, line numbers, and execution controls

- **ResultsPane** (`src/KustoTerminal.UI/Panes/ResultsPane.cs`)
  - Extends: `BasePane`
  - Purpose: Bottom-right panel displaying query results in table view or time chart, with column filtering and search

- **BasePane** (`src/KustoTerminal.UI/Panes/BasePane.cs`)
  - Extends: `View`
  - Purpose: Abstract base class for panes; provides FocusChanged event

### Controls (Custom UI Components)
- **SafeTextView** (`src/KustoTerminal.UI/Controls/SafeTextView.cs`)
  - Extends: `TextView`
  - Purpose: Wrapper around Terminal.Gui's TextView with bug fixes for mouse event handling

- **LineNumberGutterView** (`src/KustoTerminal.UI/Controls/LineNumberGutterView.cs`)
  - Extends: `View`
  - Purpose: Line number gutter displayed to left of editor; syncs scroll position with TextEditor

### Custom Views
- **TimeChartView** (`src/KustoTerminal.UI/Charts/TimeChartView.cs`)
  - Extends: `View`
  - Purpose: Renders time-series data using Braille Unicode characters for sub-character resolution plotting

### Dialogs
- **ConnectionDialog** (`src/KustoTerminal.UI/Dialogs/ConnectionDialog.cs`)
  - Extends: `Dialog`
  - Purpose: Modal dialog to add/edit connections; captures cluster URI, database, name, auth type

- **ColumnSelectorDialog** (`src/KustoTerminal.UI/Dialogs/ColumnSelectorDialog.cs`)
  - Extends: `Dialog`
  - Purpose: Modal dialog to select which columns to display from result table

- **JsonTreeViewDialog** (`src/KustoTerminal.UI/Dialogs/JsonTreeViewDialog.cs`)
  - Extends: `Dialog`
  - Purpose: Modal dialog to view/navigate JSON data as expandable tree structure
  - Also defines: `JsonTreeNode` and `PropertyDetailsDialog`

- **ShareDialog** (`src/KustoTerminal.UI/Dialogs/ShareDialog.cs`)
  - Extends: `Dialog`
  - Purpose: Modal dialog to select what to copy to clipboard (query and/or results)

### Tree Nodes (UI Models)
- **ClusterTreeNode** (`src/KustoTerminal.UI/Models/ClusterTreeNode.cs`)
  - Extends: `TreeNode`
  - Purpose: Tree node representing a cluster in the connection tree

- **DatabaseTreeNode** (`src/KustoTerminal.UI/Models/ClusterTreeNode.cs`)
  - Extends: `TreeNode`
  - Purpose: Tree node representing a database under a cluster

---

## 2. SCREEN COMPOSITION / ENTRY POINTS

### Application Entry Point
**`src/KustoTerminal.CLI/Program.cs`** (Main method)
```csharp
static async Task Main(string[] args)
{
    // 1. Initialize services
    var connectionManager = new ConnectionManager();
    var userSettingsManager = new UserSettingsManager();
    await connectionManager.LoadConnectionsAsync();
    
    // 2. Initialize Terminal.Gui (with custom driver or fallback)
    ConfigurationManager.Enable(ConfigLocations.All);
    var driver = new KustoConsoleDriver(); // with fallback to NetDriver
    Application.Init(driver: driver);
    
    // 3. Create and run main window
    using var window = MainWindow.Create(connectionManager, userSettingsManager);
    Application.Run(window);
}
```

### MainWindow Layout Assembly
**`src/KustoTerminal.UI/MainWindow.cs`** - Constructor flow:
1. **InitializeComponents()** (line 85-163)
   - Creates 3 FrameViews: `_leftFrame` (Connections), `_rightTopFrame` (Query Editor), `_rightBottomFrame` (Results)
   - Creates 3 Panes: `ConnectionPane`, `QueryEditorPane`, `ResultsPane`
   - Creates status labels and shortcuts

2. **SetupLayout()** (line 181-209)
   ```csharp
   Add(_leftFrame, _rightTopFrame, _rightBottomFrame, _kustoTerminalLabel);
   _leftFrame.Add(_connectionPane);        // Add pane to frame
   _rightTopFrame.Add(_queryEditorPane);   // Add pane to frame
   _rightBottomFrame.Add(_resultsPane);    // Add pane to frame
   ```
   - Subscribes to pane events:
     - `_connectionPane.ConnectionSelected`
     - `_queryEditorPane.QueryExecuteRequested`
     - `_queryEditorPane.QueryCancelRequested`
     - `_queryEditorPane.MaximizeToggleRequested`
     - `_resultsPane.MaximizeToggleRequested`

3. **SetupClusterSchemaEvents()** (line 573-600)
   - Subscribes to `_connectionManager.ConnectionAddOrUpdated` events
   - Preloads cluster schemas on startup

4. **SetKeyboard()** (line 211-273)
   - Ctrl+Q/Ctrl+C: Quit
   - F12: Toggle pane maximize
   - Alt+CursorRight/Left/Up/Down: Switch focus between panes

---

## 3. EVENT/MESSAGING SYSTEM

This is a **direct event-driven architecture** (no centralized event bus). Components communicate via:

### Primary Events

**ConnectionPane Events:**
```csharp
public event EventHandler<KustoConnection>? ConnectionSelected;
```

**QueryEditorPane Events:**
```csharp
public event EventHandler<string>? QueryExecuteRequested;      // Carries query text
public event EventHandler? QueryCancelRequested;
public event EventHandler? MaximizeToggleRequested;
```

**ResultsPane Events:**
```csharp
public event EventHandler? MaximizeToggleRequested;
```

**IConnectionManager Interface Events:**
```csharp
public event EventHandler<KustoConnection>? ConnectionAddOrUpdated;
```

### Event Flow Example: Query Execution
1. User types in `QueryEditorPane` and presses Ctrl+Enter
2. `QueryEditorPane.QueryExecuteRequested` event fires with query text
3. `MainWindow.OnQueryExecuteRequested()` handler (line 287-289) catches it
4. Calls `ExecuteQueryAsync()` which:
   - Gets selected connection from `ConnectionPane`
   - Creates `KustoClient` with connection
   - Calls `KustoClient.ExecuteQueryAsync()`
   - Passes progress callback to update UI: `Application.Invoke(() => ...)`
   - On completion: calls `_resultsPane.DisplayResult(result)`

### No Pub/Sub System
- No MessageBus, EventBus, or broker pattern
- No dependency injection container (services instantiated directly)
- Direct parent→child event subscription in `MainWindow.SetupLayout()`

---

## 4. TAB-LIKE / MULTI-DOCUMENT INTERFACE

**NO TAB SYSTEM EXISTS.**

The current architecture is **single-query focused**:
- One query editor pane
- One results pane
- Results from the last executed query are displayed
- Previous results are lost when a new query is executed

**Potential for tabs would require:**
- Modify `MainWindow` to host multiple `QueryEditorPane` instances
- Track current active tab
- Store results per-query-tab
- Add tab bar UI above query editor

---

## 5. CONNECTION MODEL

### KustoConnection Class
**`src/KustoTerminal.Core/Models/KustoConnection.cs`**
```csharp
public class KustoConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string ClusterUri { get; set; } = string.Empty;      // e.g., https://cluster.kusto.windows.net
    public string Database { get; set; } = string.Empty;
    public List<string> Databases { get; set; } = new List<string>();
    public AuthenticationType AuthType { get; set; } = AuthenticationType.AzureCli;
    
    public bool IsValid() { /* Validates URI and database */ }
    public string GetClusterNameFromUrl() { /* Extracts host */ }
}

public enum AuthenticationType { None, AzureCli }
```

### Connection Manager
**`src/KustoTerminal.Core/Services/ConnectionManager.cs`**
- Stores connections in JSON file: `%APPDATA%\KustoTerminal\connections.json`
- Implements `IConnectionManager`
- Methods: `AddConnectionAsync()`, `UpdateConnectionAsync()`, `DeleteConnectionAsync()`, `GetConnectionsAsync()`
- Event: `ConnectionAddOrUpdated` fires when connection is added/updated

### Connection Lifecycle
1. User clicks "Ctrl+N" in `ConnectionPane`
2. `ConnectionDialog` modal opens
3. User enters cluster URI, database name, auth type
4. Dialog validates and returns `KustoConnection`
5. `ConnectionManager.AddConnectionAsync()` saves to file + fires event
6. `MainWindow` catches event and calls `ClusterSchemaService.FetchAndUpdateClusterSchemaAsync()`

---

## 6. QUERY EXECUTION FLOW

### Complete Flow Trace

**1. User Input** (QueryEditorPane)
- User types Kusto query in editor
- Presses Ctrl+Enter
- `QueryEditorPane.OnKeyDown()` detects shortcut
- Fires: `QueryExecuteRequested?.Invoke(this, query)`

**2. Event Routing** (MainWindow)
```csharp
// Line 287-289: Event handler
private async void OnQueryExecuteRequested(object? sender, string query)
{
    await ExecuteQueryAsync(query);
}
```

**3. Query Execution** (MainWindow.ExecuteQueryAsync, line 453-540)
```csharp
// Step 1: Get selected connection from ConnectionPane
var connection = _connectionPane.GetSelectedConnection();

// Step 2: Create authentication provider
var authProvider = AuthenticationProviderFactory.CreateProvider(connection.AuthType)!;

// Step 3: Create KustoClient
_currentKustoClient = new Core.Services.KustoClient(connection, authProvider);

// Step 4: Execute query with progress callback
var result = await _currentKustoClient.ExecuteQueryAsync(
    query, 
    cancellationToken, 
    progress: new Progress<string>(message =>
    {
        Application.Invoke(() => _queryEditorPane.UpdateProgressMessage(message));
    })
);

// Step 5: Display results on UI thread
Application.Invoke(() =>
{
    _queryEditorPane.SetExecuting(false);
    _resultsPane.SetQueryText(query);
    _resultsPane.SetConnection(connection);
    _resultsPane.DisplayResult(result);
});
```

**4. KustoClient Execution** (src/KustoTerminal.Core/Services/KustoClient.cs, line 32-100)
```csharp
public async Task<QueryResult> ExecuteQueryAsync(
    string query, 
    CancellationToken cancellationToken = default, 
    IProgress<string>? progress = null)
{
    // 1. Ensure connection is initialized
    progress?.Report("Initializing connection...");
    await EnsureConnectionAsync();
    
    // 2. Prepare request properties
    var clientRequestProperties = new ClientRequestProperties();
    _currentRequestId = $"KustoTerminal;{Guid.NewGuid().ToString()}";
    clientRequestProperties.ClientRequestId = _currentRequestId;
    
    // 3. Execute query or command (dot-commands → admin provider, queries → query provider)
    progress?.Report("Executing query...");
    IDataReader reader;
    if (isCommand)
        reader = await _adminProvider!.ExecuteControlCommandAsync(_connection.Database, query, clientRequestProperties);
    else
        reader = await _queryProvider!.ExecuteQueryAsync(_connection.Database, query, clientRequestProperties);
    
    // 4. Load results into DataTable
    progress?.Report("Processing results...");
    var dataTable = new DataTable();
    dataTable.Load(reader);
    
    // 5. Detect if result is time chart and return QueryResult
    var isTimeChart = TimeChartDetector.IsTimeChartData(dataTable);
    return new QueryResult { DataTable = dataTable, IsTimeChart = isTimeChart, ... };
}
```

**5. Results Display** (ResultsPane.DisplayResult)
- Populates `TableView` with `DataTable` from result
- If `IsTimeChart` detected, also renders `TimeChartView`
- Updates status label with row/column counts
- Allows column filtering via `ColumnSelectorDialog`

### Cancellation Flow
- User presses Ctrl+C in `QueryEditorPane`
- Fires: `QueryCancelRequested`
- `MainWindow.OnQueryCancelRequested()` calls `_queryCancellationTokenSource.Cancel()`
- `KustoClient` detects cancellation and throws `OperationCanceledException`
- UI updates: `_queryEditorPane.SetExecuting(false)`

---

## Services & Supporting Classes

### Core Services (src/KustoTerminal.Core/Services/)
- **KustoClient**: Executes queries via Kusto.Data SDK
- **ConnectionManager**: Persists/loads connections from JSON
- **ClusterSchemaService**: Fetches cluster schemas for autocomplete
- **TimeChartDetector**: Detects time-series result patterns
- **AuthenticationProviderFactory**: Creates auth providers

### UI Services (src/KustoTerminal.UI/)
- **SyntaxHighlighter**: Syntax coloring for Kusto queries
- **HtmlSyntaxHighlighter**: HTML rendering of results with syntax highlighting
- **AutocompleteSuggestionGenerator**: Provides autocomplete suggestions
- **ClipboardService**: Copy results to clipboard

### Language Service (src/KustoTerminal.Language/)
- **LanguageService**: Kusto language parsing and classification

---

## Key Dependencies
- **Terminal.Gui v2.0+**: TUI framework
- **Kusto.Data**: Official SDK for Kusto/ADX queries
- **Newtonsoft.Json**: Connection serialization
- Custom **KustoConsoleDriver**: macOS-optimized input/rendering

