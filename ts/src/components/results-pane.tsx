import React, { useState } from "react";
import { Box, Text, useInput } from "ink";
import type { QueryResult } from "../core/models.js";
import { TableView } from "./table-view.js";

interface ResultsPaneProps {
  result: QueryResult | null;
  isActive: boolean;
  isQueryRunning: boolean;
  error: string | null;
}

export function ResultsPane({
  result,
  isActive,
  isQueryRunning,
  error,
}: ResultsPaneProps) {
  const [scrollOffset, setScrollOffset] = useState(0);

  const maxVisibleRows = Math.max(
    1,
    (process.stdout.rows ?? 24) - 20,
  );

  useInput(
    (input, key) => {
      if (!isActive || !result) return;

      if (key.upArrow) {
        setScrollOffset((prev) => Math.max(0, prev - 1));
      } else if (key.downArrow) {
        setScrollOffset((prev) =>
          Math.min(Math.max(0, result.rowCount - maxVisibleRows), prev + 1),
        );
      } else if (key.pageUp || (input === "u" && key.ctrl)) {
        setScrollOffset((prev) => Math.max(0, prev - maxVisibleRows));
      } else if (key.pageDown || (input === "d" && key.ctrl)) {
        setScrollOffset((prev) =>
          Math.min(
            Math.max(0, result.rowCount - maxVisibleRows),
            prev + maxVisibleRows,
          ),
        );
      }
    },
  );

  // Reset scroll when result changes
  React.useEffect(() => {
    setScrollOffset(0);
  }, [result]);

  const borderColor = isActive ? "green" : "gray";

  const renderContent = () => {
    if (isQueryRunning) {
      return <Text color="yellow">⏳ Executing query...</Text>;
    }

    if (error && !result?.success) {
      return (
        <Box flexDirection="column">
          <Text color="red" bold>
            Error
          </Text>
          <Text color="red">{error}</Text>
        </Box>
      );
    }

    if (!result) {
      return <Text dimColor>No results. Execute a query to see data here.</Text>;
    }

    if (result.rowCount === 0) {
      return <Text dimColor>Query completed in {result.durationMs}ms — no rows returned.</Text>;
    }

    return (
      <Box flexDirection="column">
        <TableView
          result={result}
          scrollOffset={scrollOffset}
          maxVisibleRows={maxVisibleRows}
        />
        <Box marginTop={1}>
          <Text dimColor>
            {result.rowCount} rows ({result.durationMs}ms)
            {result.rowCount > maxVisibleRows &&
              ` | Showing ${scrollOffset + 1}-${Math.min(scrollOffset + maxVisibleRows, result.rowCount)}`}
          </Text>
        </Box>
      </Box>
    );
  };

  return (
    <Box
      flexDirection="column"
      borderStyle="round"
      borderColor={borderColor}
      paddingX={1}
      width="100%"
      height="100%"
    >
      <Text bold color="white">
        Results
      </Text>
      <Box flexDirection="column" marginTop={1} flexGrow={1}>
        {renderContent()}
      </Box>
      {isActive && result && result.rowCount > maxVisibleRows && (
        <Text dimColor>↑↓:scroll PgUp/PgDn:page</Text>
      )}
    </Box>
  );
}
