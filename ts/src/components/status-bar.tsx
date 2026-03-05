import React from "react";
import { Box, Text } from "ink";
import type { KustoConnection } from "../core/models.js";

interface StatusBarProps {
  connection: KustoConnection | null;
  isConnected: boolean;
  isQueryRunning: boolean;
  queryDurationMs?: number;
  rowCount?: number;
}

export function StatusBar({
  connection,
  isConnected,
  isQueryRunning,
  queryDurationMs,
  rowCount,
}: StatusBarProps) {
  return (
    <Box width="100%" justifyContent="space-between" paddingX={1}>
      <Box gap={2}>
        {isConnected && connection ? (
          <Text>
            <Text color="green">●</Text>{" "}
            <Text bold>{connection.name}</Text>{" "}
            <Text dimColor>({connection.database})</Text>
          </Text>
        ) : (
          <Text>
            <Text color="red">●</Text>{" "}
            <Text dimColor>Not connected</Text>
          </Text>
        )}

        {isQueryRunning && <Text color="yellow">⏳ Running</Text>}
        {!isQueryRunning && queryDurationMs !== undefined && (
          <Text dimColor>
            {rowCount} rows in {queryDurationMs}ms
          </Text>
        )}
      </Box>

      <Text dimColor>
        Alt+←→↑↓:navigate Shift+↵:run Ctrl+Q:quit
      </Text>
    </Box>
  );
}
