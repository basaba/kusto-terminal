# Kusto Terminal UI Classes - Complete List with File Paths

## All 13 UI/View Classes

### 1. MainWindow
- **File**: `src/KustoTerminal.UI/MainWindow.cs`
- **Lines**: 621
- **Extends**: `Terminal.Gui.Window`
- **Purpose**: Root application window container that assembles all panes
- **Key Methods**: 
  - Constructor (InitializeComponents, SetupLayout, SetKeyboard)
  - ExecuteQueryAsync() - Main query execution handler
  - OnQueryExecuteRequested() - Event handler for query execution
  - OnConnectionSelected() - Connection selection handler
- **Key Fields**:
  - `_leftFrame`, `_rightTopFrame`, `_rightBottomFrame` - Layout containers
  - `_connectionPane`, `_queryEditorPane`, `_resultsPane` - Main panes
  - `_currentKustoClient` - Active Kusto client
  - `_queryCancellationTokenSource` - Query cancellation token

---

### 2. ConnectionPane
- **File**: `src/KustoTerminal.UI/Panes/ConnectionPane.cs`
- **Extends**: `Terminal.Gui.View`
- **Purpose**: Left sidebar showing cluster/database tree and connection management
- **Key Components**:
  - TreeView (`_connectionsTree`) - Shows hierarchical connections
  - Shortcut labels for Ctrl+N, Ctrl+E, Del, Space
- **Key Methods**:
  - LoadConnections() - Load connections from manager
  - OnTreeSelectionChanged() - Handle tree selection
  - OnAddClicked(), OnEditClicked() - Connection dialogs
- **Events**:
  - `ConnectionSelected` - Fired when user selects a connection
- **Key Fields**:
  - `_connectionManager` - Reference to connection manager
  - `_connections` - Cached list of connections
  - `_selectedConnection` - Currently selected connection
  - `_kustoClients` - Dictionary of clients per connection

---

### 3. QueryEditorPane
- **File**: `src/KustoTerminal.UI/Panes/QueryEditorPane.cs`
- **Extends**: `BasePane`
- **Purpose**: Top-right editor panel for writing and executing Kusto queries
- **Key Components**:
  - SafeTextView (`_queryTextView`) - Query editor
  - LineNumberGutterView (`_lineNumberGutter`) - Line numbers
  - Labels for connection, progress, shortcuts
- **Key Methods**:
  - SetConnection() - Update current connection display
  - SetExecuting() - Update execution state UI
  - UpdateProgressMessage() - Update progress label
  - SetupAutocomplete() - Initialize autocomplete
- **Events**:
  - `QueryExecuteRequested` - Fired when user presses Ctrl+Enter
  - `QueryCancelRequested` - Fired when user presses Ctrl+C
  - `MaximizeToggleRequested` - Fired when user presses F12
- **Key Fields**:
  - `_currentConnection` - Selected connection
  - `_isExecuting` - Query execution state
  - `_syntaxHighlighter` - Syntax highlighting service
  - `_autocompleteSuggestionGenerator` - Autocomplete provider

---

### 4. ResultsPane
- **File**: `src/KustoTerminal.UI/Panes/ResultsPane.cs`
- **Extends**: `BasePane`
- **Purpose**: Bottom-right panel for displaying query results
- **Key Components**:
  - TableView (`_tableView`) - Results table
  - TimeChartView (`_chartView`) - Time-series chart (conditional)
  - TextField (`_searchField`) - Result search/filter
  - Label (`_statusLabel`) - Row/column count display
  - SafeTextView (`_errorLabel`) - Error message display
- **Key Methods**:
  - DisplayResult() - Show query results
  - SetQueryText() - Store executing query
  - SetConnection() - Store connection
  - ApplyColumnFilter() - Show column selector dialog
  - OnSearchTextChanged() - Filter results
- **Events**:
  - `MaximizeToggleRequested` - Fired when user presses F12
- **Key Fields**:
  - `_tableView` - Results grid
  - `_chartView` - Time chart renderer
  - `_currentResult` - Current QueryResult
  - `_originalData` - Unfiltered data
  - `_selectedColumns` - Visible columns
  - `_searchVisible` - Search box visibility
  - `_chartVisible` - Chart visibility

---

### 5. BasePane
- **File**: `src/KustoTerminal.UI/Panes/BasePane.cs`
- **Extends**: `Terminal.Gui.View`
- **Purpose**: Abstract base class for pane views
- **Key Events**:
  - `FocusChanged` - Raised when focus changes (not currently implemented)
- **Notes**: Minimal class, primarily for inheritance structure

---

### 6. SafeTextView
- **File**: `src/KustoTerminal.UI/Controls/SafeTextView.cs`
- **Extends**: `Terminal.Gui.TextView`
- **Purpose**: Fixed wrapper around Terminal.Gui's TextView with bug fixes
- **Key Fixes**:
  - Catches `ArgumentOutOfRangeException` from ProcessDoubleClickSelection
  - Occurs when double-clicking at end of line
- **Events**:
  - `MouseProcessed` - Raised after mouse event handled
- **Key Methods**:
  - OnMouseEvent() - Override with exception handling

---

### 7. LineNumberGutterView
- **File**: `src/KustoTerminal.UI/Controls/LineNumberGutterView.cs`
- **Extends**: `Terminal.Gui.View`
- **Purpose**: Line number gutter displayed to left of text editor
- **Key Features**:
  - Syncs with TextView scroll position
  - Dynamic width based on line count
  - Highlights current line
- **Key Methods**:
  - SetTextView() - Bind to a TextView
  - UpdateWidth() - Recalculate gutter width
  - Redraw() - Render line numbers
- **Key Fields**:
  - `_textView` - Associated TextView
  - `_gutterWidth` - Current width in characters

---

### 8. TimeChartView
- **File**: `src/KustoTerminal.UI/Charts/TimeChartView.cs`
- **Extends**: `Terminal.Gui.View`
- **Purpose**: Renders time-series data using Braille Unicode characters
- **Key Features**:
  - Sub-character resolution using Braille characters (2 cols × 4 rows per cell)
  - Multiple colored series
  - Axis labels and legend
  - Detects time values and Y-axis ranges
- **Key Methods**:
  - SetData() - Load TimeChartData
  - Redraw() - Render the chart
- **Key Fields**:
  - `_chartData` - Current chart data
  - `SeriesColors` - Color palette for each series

---

### 9. ConnectionDialog
- **File**: `src/KustoTerminal.UI/Dialogs/ConnectionDialog.cs`
- **Extends**: `Terminal.Gui.Dialog`
- **Purpose**: Modal dialog to add or edit a connection
- **Key Components**:
  - TextField for Name, ClusterUri, Database
  - RadioGroup for AuthType selection
  - OK/Cancel buttons
- **Key Methods**:
  - InitializeComponents() - Build form fields
  - SetupLayout() - Add controls to dialog
  - OK/Cancel click handlers
- **Key Properties**:
  - `Result` - Returns KustoConnection if OK clicked, null if cancelled
- **Window Size**: 60 chars wide × 18 lines tall

---

### 10. ColumnSelectorDialog
- **File**: `src/KustoTerminal.UI/Dialogs/ColumnSelectorDialog.cs`
- **Extends**: `Terminal.Gui.Dialog`
- **Purpose**: Modal dialog to select which result columns to display
- **Key Components**:
  - ListView - Checklist of columns
  - Select All / Deselect All buttons
- **Key Properties**:
  - `SelectedColumns` - HashSet<string> of selected column names
- **Window Size**: 60 chars wide × dynamic height based on column count (max 30 lines)

---

### 11. JsonTreeViewDialog
- **File**: `src/KustoTerminal.UI/Dialogs/JsonTreeViewDialog.cs`
- **Extends**: `Terminal.Gui.Dialog`
- **Purpose**: Modal dialog to view and explore JSON data in tree structure
- **Key Components**:
  - TreeView - Expandable JSON structure
  - StatusLabel - Shows selected node info
- **Also Defines**:
  - `JsonTreeNode` (extends TreeNode) - Tree node for JSON objects/arrays
  - `PropertyDetailsDialog` - Details view for JSON properties
- **Window Size**: 100 chars wide × 30 lines tall
- **Key Methods**:
  - LoadJsonData() - Parse JSON and populate tree
  - OnNodeSelected() - Show property details

---

### 12. ShareDialog
- **File**: `src/KustoTerminal.UI/Dialogs/ShareDialog.cs`
- **Extends**: `Terminal.Gui.Dialog`
- **Purpose**: Modal dialog to select what to copy to clipboard
- **Key Components**:
  - CheckBox for "Copy Query"
  - CheckBox for "Copy Results"
  - OK/Cancel buttons
- **Key Properties**:
  - `CopyQuery` - Include query in clipboard
  - `CopyResult` - Include results in clipboard
  - `WasCanceled` - User clicked Cancel
- **Window Size**: 50 chars wide × 10 lines tall

---

### 13. Tree Node Classes (UI Models)

#### ClusterTreeNode
- **File**: `src/KustoTerminal.UI/Models/ClusterTreeNode.cs`
- **Extends**: `Terminal.Gui.TreeNode`
- **Purpose**: Tree node representing a cluster connection
- **Key Properties**:
  - `Connection` - Associated KustoConnection
  - `IsExpandable` - True (always shows children)

#### DatabaseTreeNode
- **File**: `src/KustoTerminal.UI/Models/ClusterTreeNode.cs`
- **Extends**: `Terminal.Gui.TreeNode`
- **Purpose**: Tree node representing a database in a cluster
- **Key Properties**:
  - `DatabaseName` - Database name
  - `IsExpandable` - True (can show tables)

---

## Summary Table

| # | Class Name | File | Type | Purpose |
|---|------------|------|------|---------|
| 1 | MainWindow | UI/MainWindow.cs | Window | Root container, orchestrator |
| 2 | ConnectionPane | UI/Panes/ConnectionPane.cs | View (Pane) | Connection tree, selection |
| 3 | QueryEditorPane | UI/Panes/QueryEditorPane.cs | View (Pane) | Query editor, execution trigger |
| 4 | ResultsPane | UI/Panes/ResultsPane.cs | View (Pane) | Results table, charts |
| 5 | BasePane | UI/Panes/BasePane.cs | View | Abstract base for panes |
| 6 | SafeTextView | UI/Controls/SafeTextView.cs | Control | Fixed TextView |
| 7 | LineNumberGutterView | UI/Controls/LineNumberGutterView.cs | Control | Line number gutter |
| 8 | TimeChartView | UI/Charts/TimeChartView.cs | Control | Time-series chart |
| 9 | ConnectionDialog | UI/Dialogs/ConnectionDialog.cs | Dialog | Add/edit connection |
| 10 | ColumnSelectorDialog | UI/Dialogs/ColumnSelectorDialog.cs | Dialog | Select result columns |
| 11 | JsonTreeViewDialog | UI/Dialogs/JsonTreeViewDialog.cs | Dialog | Explore JSON data |
| 12 | ShareDialog | UI/Dialogs/ShareDialog.cs | Dialog | Copy to clipboard |
| 13 | ClusterTreeNode | UI/Models/ClusterTreeNode.cs | TreeNode | Connection tree item |
| 14 | DatabaseTreeNode | UI/Models/ClusterTreeNode.cs | TreeNode | Database tree item |

(Note: TreeNode classes listed as 13-14 but often counted as one entry due to being in same file)

---

## Architecture Overview

```
MainWindow (Window)
├── _leftFrame (FrameView)
│   └── ConnectionPane (View)
│       └── TreeView
│           ├── ClusterTreeNode
│           ├── DatabaseTreeNode
│           └── [TableNode - implicit]
│
├── _rightTopFrame (FrameView)
│   └── QueryEditorPane (BasePane → View)
│       ├── SafeTextView (_queryTextView)
│       ├── LineNumberGutterView (_lineNumberGutter)
│       ├── Label (_connectionLabel)
│       ├── Label (_progressLabel)
│       └── [Autocomplete popup - implicit]
│
└── _rightBottomFrame (FrameView)
    └── ResultsPane (BasePane → View)
        ├── TableView (_tableView)
        ├── TimeChartView (_chartView) [conditional]
        ├── TextField (_searchField)
        ├── Label (_statusLabel)
        └── SafeTextView (_errorLabel)

Dialogs (Modal, spawned as needed):
├── ConnectionDialog
├── ColumnSelectorDialog
├── JsonTreeViewDialog
│   └── PropertyDetailsDialog [nested]
└── ShareDialog
```

