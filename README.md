# Kusto Terminal

A terminal-style Azure Data Explorer (Kusto) client inspired by k9s, built with .NET 8 and Terminal.Gui.

## Features

### Current (MVP)
- **Connection Management**: Add, edit, delete, and manage Kusto cluster connections
- **Azure CLI Authentication**: Seamless authentication using `az login`
- **Interactive Query Editor**: Multi-line KQL query editor with basic shortcuts
- **Results Display**: Tabular data presentation with export capabilities
- **Terminal UI**: Full-screen interface with panes and navigation
- **Configuration Persistence**: Connections saved locally for reuse

### Planned
- Advanced query features (syntax highlighting, autocomplete)
- Query history and favorites
- Schema browsing and exploration
- Performance analysis tools
- Additional authentication methods
- Cluster management features

## Prerequisites

- .NET 8 SDK
- Azure CLI (`az` command)
- Valid Azure credentials with access to Kusto clusters

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

## Authentication Setup

Before using Kusto Terminal, ensure you're authenticated with Azure CLI:

```bash
az login
```

The application will verify your authentication on startup and guide you if authentication is required.

## Usage

### Getting Started

1. **Launch**: Run the application from the command line
2. **Add Connection**: Press `Ctrl+N` or use the File menu to add a new Kusto connection
3. **Connect**: Select a connection from the left pane and click "Connect"
4. **Query**: Write your KQL queries in the editor pane
5. **Execute**: Press `F5` or click "Execute" to run queries
6. **Results**: View results in the bottom pane, export if needed

### Keyboard Shortcuts

- **F5**: Execute current query
- **Ctrl+N**: New connection
- **Ctrl+Q**: Quit application
- **Ctrl+C**: Copy
- **Ctrl+V**: Paste
- **Tab**: Navigate between panes
- **F1**: Show help

### Connection Configuration

When adding a connection, provide:
- **Name**: Friendly name for the connection (optional)
- **Cluster URI**: Full URI to your Kusto cluster (e.g., `https://help.kusto.windows.net`)
- **Database**: Default database name
- **Default**: Check to make this the default connection

## Architecture

The application follows a modular architecture:

```
KustoTerminal/
├── src/
│   ├── KustoTerminal.Core/        # Business logic and models
│   ├── KustoTerminal.UI/          # Terminal.Gui components
│   ├── KustoTerminal.Auth/        # Authentication providers
│   └── KustoTerminal.CLI/         # Main application entry point
```

### Key Components

- **Connection Manager**: Handles connection storage and management
- **Authentication Provider**: Azure CLI integration for secure access
- **Kusto Client**: Query execution and cluster communication
- **TUI Components**: Interactive panes for connections, queries, and results

## Configuration

Connections are stored in `%APPDATA%/KustoTerminal/connections.json` (Windows) or `~/.config/KustoTerminal/connections.json` (Linux/macOS).

## Export Formats

Query results can be exported in multiple formats:
- **CSV**: Comma-separated values
- **TSV**: Tab-separated values  
- **JSON**: JavaScript Object Notation

## Development

### Building
```bash
dotnet build
```

### Running
```bash
dotnet run --project src/KustoTerminal.CLI
```

### Testing
```bash
dotnet test
```

## Contributing

This project is in active development. Contributions are welcome!

## License

MIT License - see LICENSE file for details.

## Inspiration

This project is inspired by [k9s](https://k9scli.io/), the excellent Kubernetes CLI tool, and aims to bring similar terminal-style efficiency to Azure Data Explorer workflows.