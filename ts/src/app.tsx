import React, { useState, useCallback } from "react";
import { Box, useInput, useApp } from "ink";
import { ConnectionPane } from "./components/connection-pane.js";
import { QueryEditor } from "./components/query-editor.js";
import { ResultsPane } from "./components/results-pane.js";
import { StatusBar } from "./components/status-bar.js";
import { useFocusManager } from "./hooks/use-focus-manager.js";
import { useConnections } from "./hooks/use-connections.js";
import { useKusto } from "./hooks/use-kusto.js";
import type { KustoConnection } from "./core/models.js";

export function App() {
  const { exit } = useApp();
  const { activePane, setActivePane, moveLeft, moveRight, moveUp, moveDown } =
    useFocusManager("connections");
  const {
    connections,
    addQuickConnection,
    removeConnection,
  } = useConnections();
  const {
    isConnected,
    isQueryRunning,
    queryResult,
    error,
    connect,
    executeQuery,
    disconnect,
  } = useKusto();

  const [activeConnection, setActiveConnection] =
    useState<KustoConnection | null>(null);
  const [isAddingConnection, setIsAddingConnection] = useState(false);

  // Global keybindings
  useInput((input, key) => {
    // Ctrl+Q or Ctrl+C: quit
    if ((input === "q" && key.ctrl) || (input === "c" && key.ctrl)) {
      exit();
      return;
    }

    // Alt+Arrow: navigate panes
    if (key.meta) {
      if (key.leftArrow) moveLeft();
      else if (key.rightArrow) moveRight();
      else if (key.upArrow) moveUp();
      else if (key.downArrow) moveDown();
    }

    // Tab: cycle panes forward
    if (key.tab && !key.shift) {
      if (activePane === "connections") setActivePane("query");
      else if (activePane === "query") setActivePane("results");
      else setActivePane("connections");
    }
  });

  const handleConnect = useCallback(
    async (connection: KustoConnection) => {
      setActiveConnection(connection);
      await connect(connection);
      setActivePane("query");
    },
    [connect, setActivePane],
  );

  const handleDelete = useCallback(
    async (id: string) => {
      if (activeConnection?.id === id) {
        disconnect();
        setActiveConnection(null);
      }
      await removeConnection(id);
    },
    [activeConnection, disconnect, removeConnection],
  );

  const handleAddStart = useCallback(() => {
    setIsAddingConnection(true);
  }, []);

  const handleAddComplete = useCallback(
    async (clusterUri: string, database: string) => {
      const conn = await addQuickConnection(clusterUri, database);
      setIsAddingConnection(false);
      await handleConnect(conn);
    },
    [addQuickConnection, handleConnect],
  );

  const handleAddCancel = useCallback(() => {
    setIsAddingConnection(false);
  }, []);

  const handleExecuteQuery = useCallback(
    (query: string) => {
      executeQuery(query);
    },
    [executeQuery],
  );

  const handleCancelQuery = useCallback(() => {
    // Query cancellation would need AbortController support in the SDK
  }, []);

  return (
    <Box flexDirection="column" width="100%" height="100%">
      <Box flexGrow={1} width="100%">
        {/* Left pane: Connections (30%) */}
        <Box width="30%" height="100%">
          <ConnectionPane
            connections={connections}
            activeConnection={activeConnection}
            isActive={activePane === "connections"}
            isAddingMode={isAddingConnection}
            onConnect={handleConnect}
            onDelete={handleDelete}
            onAddStart={handleAddStart}
            onAddComplete={handleAddComplete}
            onAddCancel={handleAddCancel}
          />
        </Box>

        {/* Right column: Query + Results (70%) */}
        <Box flexDirection="column" width="70%" height="100%">
          {/* Query Editor (60% of right) */}
          <Box height="60%">
            <QueryEditor
              isActive={activePane === "query"}
              isQueryRunning={isQueryRunning}
              onExecute={handleExecuteQuery}
              onCancel={handleCancelQuery}
            />
          </Box>

          {/* Results (40% of right) */}
          <Box height="40%">
            <ResultsPane
              result={queryResult}
              isActive={activePane === "results"}
              isQueryRunning={isQueryRunning}
              error={error}
            />
          </Box>
        </Box>
      </Box>

      {/* Status bar */}
      <StatusBar
        connection={activeConnection}
        isConnected={isConnected}
        isQueryRunning={isQueryRunning}
        queryDurationMs={queryResult?.durationMs}
        rowCount={queryResult?.rowCount}
      />
    </Box>
  );
}
