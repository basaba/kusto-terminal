# Kusto Terminal

A terminal-style Azure Data Explorer (Kusto) client, built with .NET 8 and Terminal.Gui.

## Prerequisites

- Azure CLI (`az login` command)

## Installation

1. Clone the repository
2. Build the solution:
   ```bash
   dotnet build
   ```
3. Run the application:
   ```bash
   dotnet run --project src/KustoTerminal.CLI
   ```

## Recall the last execution

Place the cursor in a query and press `F8` to restore its latest successful
result for the active cluster and database. Kusto Terminal loads the result
table and execution metadata from the local cache without sending a request to
Kusto or changing any text in the query editor.

Cached executions are stored under `~/.kusto-terminal/history`, retained for 30
days, and limited to the newest 1,000 entries. Query results can contain
sensitive data, so protect this directory like any other local data export.