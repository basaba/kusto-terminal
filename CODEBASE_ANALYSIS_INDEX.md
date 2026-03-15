# Kusto Terminal Codebase Analysis - Complete Index

This directory contains comprehensive documentation of the Kusto Terminal architecture, UI structure, and execution flows.

## 📄 Documentation Files

### 1. **QUICK_REFERENCE.md** (START HERE - 7.8 KB)
   - **Purpose**: Quick lookup guide with tables and concise explanations
   - **Contents**:
     - View/Window classes summary table
     - Screen composition entry points
     - Event/messaging system overview
     - Tab system status (doesn't exist)
     - Connection model definition
     - Query execution flow (simplified)
     - Supporting services table
     - Key files and keyboard shortcuts
   - **Best For**: Quick answers, looking up specific components

### 2. **ARCHITECTURE_ANALYSIS.md** (DETAILED - 13 KB)
   - **Purpose**: In-depth architectural documentation with code examples
   - **Contents**:
     1. **All View/Window Classes** - Complete list with file paths and descriptions
     2. **Screen Composition** - Entry points and layout assembly
     3. **Event/Messaging System** - Event-driven architecture details
     4. **Tab-like Features** - Why no tab system exists + how to add one
     5. **Connection Model** - KustoConnection class structure and lifecycle
     6. **Query Execution Flow** - Step-by-step trace from UI to execution
     7. **Services & Supporting Classes** - All backend services
     8. **Key Dependencies** - External libraries used
   - **Best For**: Understanding architecture, implementation details, code flows

## 🎯 Quick Navigation

### By Question Type

**"What are all the UI components?"**
→ See QUICK_REFERENCE.md section 1 or ARCHITECTURE_ANALYSIS.md section 1

**"Where does the app start and how is the screen assembled?"**
→ See QUICK_REFERENCE.md section 2 or ARCHITECTURE_ANALYSIS.md section 2

**"How do components communicate with each other?"**
→ See QUICK_REFERENCE.md section 3 or ARCHITECTURE_ANALYSIS.md section 3

**"Can I add tabs or multiple queries at once?"**
→ See QUICK_REFERENCE.md section 4 or ARCHITECTURE_ANALYSIS.md section 4

**"What defines a connection and where are they stored?"**
→ See QUICK_REFERENCE.md section 5 or ARCHITECTURE_ANALYSIS.md section 5

**"How does a query go from typing to displaying results?"**
→ See QUICK_REFERENCE.md section 6 or ARCHITECTURE_ANALYSIS.md section 6

## 📂 Key Source Files (Referenced in Analysis)

```
src/
├── KustoTerminal.CLI/
│   └── Program.cs                    [100 lines] Entry point
├── KustoTerminal.Core/
│   ├── Models/
│   │   └── KustoConnection.cs        Connection definition
│   ├── Services/
│   │   ├── KustoClient.cs            Query execution via SDK
│   │   ├── ConnectionManager.cs      Connection persistence
│   │   ├── ClusterSchemaService.cs   Schema caching for autocomplete
│   │   └── TimeChartDetector.cs      Time-series detection
│   └── Interfaces/
│       ├── IConnectionManager.cs
│       └── IKustoClient.cs
├── KustoTerminal.Language/
│   └── Services/
│       └── LanguageService.cs        KQL parsing & classification
└── KustoTerminal.UI/
    ├── MainWindow.cs                 [621 lines] Root UI container
    ├── Panes/
    │   ├── BasePane.cs
    │   ├── ConnectionPane.cs         [300+ lines] Connection tree UI
    │   ├── QueryEditorPane.cs        [300+ lines] Editor UI
    │   └── ResultsPane.cs            [400+ lines] Results display
    ├── Dialogs/
    │   ├── ConnectionDialog.cs       Add/edit connections
    │   ├── ColumnSelectorDialog.cs   Filter result columns
    │   ├── JsonTreeViewDialog.cs     JSON explorer
    │   └── ShareDialog.cs            Copy results
    ├── Controls/
    │   ├── SafeTextView.cs           Fixed TextView
    │   └── LineNumberGutterView.cs   Line numbers
    ├── Charts/
    │   └── TimeChartView.cs          Time-series renderer
    ├── Models/
    │   └── ClusterTreeNode.cs        Tree nodes
    ├── Services/
    │   ├── ClipboardService.cs
    │   └── SyntaxHighlighter.cs
    └── AutoCompletion/
        └── AutocompleteSuggestionGenerator.cs
```

## 🔑 Key Concepts

### Architecture Type
- **Event-Driven**: Direct component communication via .NET EventHandler<T>
- **No Central Bus**: No MessageBus, EventBus, or Pub/Sub system
- **No Dependency Injection**: Services instantiated directly

### Screen Layout
- **3-Panel Design**: Left (Connections) | Top-Right (Editor) | Bottom-Right (Results)
- **Maximize Support**: Can maximize editor or results pane via F12
- **Focus Navigation**: Alt+Cursor keys switch between panes

### Event Flow Example
```
User Input → QueryEditorPane.QueryExecuteRequested event 
           → MainWindow.OnQueryExecuteRequested() handler 
           → MainWindow.ExecuteQueryAsync() 
           → KustoClient.ExecuteQueryAsync() 
           → ResultsPane.DisplayResult()
```

### Connection Lifecycle
```
User Ctrl+N → ConnectionDialog → ConnectionManager.AddAsync() 
            → Save to connections.json 
            → Fire ConnectionAddOrUpdated event 
            → ClusterSchemaService preloads schemas
```

### Query Execution Lifecycle
```
Type query → Ctrl+Enter → KustoClient creates request ID
          → Kusto.Data SDK executes 
          → Progress updates UI via Application.Invoke() 
          → DataTable loaded, time-chart detection 
          → Results displayed in TableView or TimeChartView
```

## 🚫 What Doesn't Exist

- **Tab System**: No multi-document interface. Single query/result pair at a time.
- **Pub/Sub Bus**: Components directly subscribe to events from panes.
- **Dependency Injection**: No IoC container (though services are injected as constructor params).
- **Workspace/Session Management**: No way to save query sessions or organize multiple queries.

## 💡 Adding Tabs (Implementation Guide)

To add a tab system, you would need to:

1. **Create QueryTab Model**
   ```csharp
   public class QueryTab
   {
       public string Id { get; set; }
       public QueryEditorPane Editor { get; set; }
       public QueryResult Result { get; set; }
   }
   ```

2. **Modify MainWindow**
   - Replace single `_queryEditorPane` with `List<QueryTab> _tabs`
   - Add `int _activeTabIndex`
   - Create TabView/TabBar above editor
   - Route events to active tab's editor pane

3. **Add Tab Operations**
   - New Tab: Ctrl+T
   - Close Tab: Ctrl+W
   - Switch Tab: Ctrl+Tab / Ctrl+Shift+Tab

4. **Store Results Per Tab**
   - Each tab keeps its own QueryResult
   - ResultsPane displays active tab's result

## 📊 Component Statistics

| Category | Count | Details |
|----------|-------|---------|
| View/Window Classes | 13 | 1 Window, 3 Panes, 3 Controls, 4 Dialogs, 2 Tree Nodes |
| Dialog Types | 4 | Connection, Columns, JSON, Share |
| Core Services | 5 | KustoClient, ConnectionManager, ClusterSchemaService, TimeChartDetector, AuthProvider |
| Events Defined | 5 | ConnectionSelected, QueryExecuteRequested, QueryCancelRequested, MaximizeToggled, ConnectionAddOrUpdated |
| Entry Points | 1 | Program.Main() |
| Main Files >200 Lines | 5 | MainWindow, QueryEditorPane, ResultsPane, KustoClient, various |

## 🔍 How to Use This Documentation

1. **First Time?** Read QUICK_REFERENCE.md section 1-3 for overview
2. **Deep Dive?** Read ARCHITECTURE_ANALYSIS.md sections 5-6 for connection & query flow
3. **Implementing Tabs?** Check section 4 in both files for guidance
4. **Finding a Specific File?** Use the Key Source Files tree above
5. **Understanding an Event?** Search for event name in both docs

## 📚 Additional Resources

- Terminal.Gui Documentation: https://github.com/gui-cs/Terminal.Gui
- Kusto.Data SDK: Microsoft.Azure.Kusto.Data NuGet package
- Kusto Query Language: https://learn.microsoft.com/en-us/azure/data-explorer/kusto/query/

