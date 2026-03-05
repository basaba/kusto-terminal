import React, { useState } from "react";
import { Box, Text, useInput } from "ink";

interface CellDetailProps {
  columnName: string;
  columnType: string;
  value: string;
  rowIndex: number;
  onClose: () => void;
}

export function CellDetail({
  columnName,
  columnType,
  value,
  rowIndex,
  onClose,
}: CellDetailProps) {
  const [scrollOffset, setScrollOffset] = useState(0);
  const valueLines = value.split("\n");
  const maxVisible = Math.max(1, (process.stdout.rows ?? 24) - 26);

  useInput((input, key) => {
    if (key.escape || key.return) {
      onClose();
      return;
    }
    if (key.upArrow) {
      setScrollOffset((prev) => Math.max(0, prev - 1));
    } else if (key.downArrow) {
      setScrollOffset((prev) =>
        Math.min(Math.max(0, valueLines.length - maxVisible), prev + 1),
      );
    }
  });

  const visibleLines = valueLines.slice(scrollOffset, scrollOffset + maxVisible);

  return (
    <Box
      flexDirection="column"
      borderStyle="round"
      borderColor="yellow"
      paddingX={1}
    >
      <Box gap={2}>
        <Text bold color="yellow">
          Cell Detail
        </Text>
        <Text dimColor>
          Row {rowIndex + 1}
        </Text>
      </Box>

      <Box marginTop={1} gap={1}>
        <Text bold>Column:</Text>
        <Text color="cyan">{columnName}</Text>
        <Text dimColor>({columnType})</Text>
      </Box>

      <Box flexDirection="column" marginTop={1}>
        <Text bold>Value:</Text>
        {visibleLines.map((line, i) => (
          <Text key={scrollOffset + i} wrap="wrap">{line}</Text>
        ))}
        {valueLines.length > maxVisible && (
          <Text dimColor>
            Lines {scrollOffset + 1}-{Math.min(scrollOffset + maxVisible, valueLines.length)} of {valueLines.length}
          </Text>
        )}
      </Box>

      <Box marginTop={1}>
        <Text dimColor>Esc/↵:close ↑↓:scroll</Text>
      </Box>
    </Box>
  );
}
