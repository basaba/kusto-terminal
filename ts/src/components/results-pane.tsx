import React, { useState } from "react";
import { Box, Text, useInput } from "ink";
import type { QueryResult } from "../core/models.js";
import { TableView, formatCellValue } from "./table-view.js";
import { CellDetail } from "./cell-detail.js";

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
  const [selectedRow, setSelectedRow] = useState(0);
  const [selectedCol, setSelectedCol] = useState(0);
  const [colScrollOffset, setColScrollOffset] = useState(0);
  const [showCellDetail, setShowCellDetail] = useState(false);

  const maxVisibleRows = Math.max(
    1,
    (process.stdout.rows ?? 24) - 20,
  );

  const colCount = result?.columns.length ?? 0;
  const rowCount = result?.rowCount ?? 0;

  // Ensure selectedCol scrolls into view
  const adjustColScroll = (col: number) => {
    setColScrollOffset((prev) => {
      if (col < prev) return col;
      // We can't easily compute visible column count here without widths,
      // so just ensure the selected column is at least at the scroll offset
      return prev;
    });
  };

  useInput(
    (input, key) => {
      if (!isActive || !result || result.rowCount === 0) return;

      // If cell detail is open, only handle Esc/Enter to close
      if (showCellDetail) {
        if (key.escape || key.return) {
          setShowCellDetail(false);
        }
        return;
      }

      // Enter: show cell detail
      if (key.return) {
        setShowCellDetail(true);
        return;
      }

      // Vertical navigation
      if (key.upArrow) {
        setSelectedRow((prev) => {
          const newRow = Math.max(0, prev - 1);
          // Auto-scroll viewport up
          setScrollOffset((off) => (newRow < off ? newRow : off));
          return newRow;
        });
      } else if (key.downArrow) {
        setSelectedRow((prev) => {
          const newRow = Math.min(rowCount - 1, prev + 1);
          // Auto-scroll viewport down
          setScrollOffset((off) =>
            newRow >= off + maxVisibleRows ? newRow - maxVisibleRows + 1 : off,
          );
          return newRow;
        });
      } else if (key.pageUp || (input === "u" && key.ctrl)) {
        setSelectedRow((prev) => {
          const newRow = Math.max(0, prev - maxVisibleRows);
          setScrollOffset((off) => Math.max(0, off - maxVisibleRows));
          return newRow;
        });
      } else if (key.pageDown || (input === "d" && key.ctrl)) {
        setSelectedRow((prev) => {
          const newRow = Math.min(rowCount - 1, prev + maxVisibleRows);
          setScrollOffset((off) =>
            Math.min(Math.max(0, rowCount - maxVisibleRows), off + maxVisibleRows),
          );
          return newRow;
        });
      }

      // Horizontal navigation
      if (key.leftArrow) {
        setSelectedCol((prev) => {
          const newCol = Math.max(0, prev - 1);
          adjustColScroll(newCol);
          return newCol;
        });
      } else if (key.rightArrow) {
        setSelectedCol((prev) => {
          const newCol = Math.min(colCount - 1, prev + 1);
          // If moving right past visible area, bump col scroll
          setColScrollOffset((off) => (newCol > off + 10 ? off + 1 : off));
          return newCol;
        });
      } else if (key.home) {
        setSelectedCol(0);
        setColScrollOffset(0);
      } else if (key.end) {
        setSelectedCol(colCount - 1);
      }
    },
  );

  // Reset selection when result changes
  React.useEffect(() => {
    setScrollOffset(0);
    setSelectedRow(0);
    setSelectedCol(0);
    setColScrollOffset(0);
    setShowCellDetail(false);
  }, [result]);

  const borderColor = isActive ? "green" : "gray";

  // Get current cell info for detail view
  const getCurrentCellInfo = () => {
    if (!result || result.rowCount === 0) return null;
    const col = result.columns[selectedCol];
    const row = result.rows[selectedRow];
    if (!col || !row) return null;
    return {
      columnName: col.name,
      columnType: col.type,
      value: formatCellValue(row[col.name]),
      rowIndex: selectedRow,
      colIndex: selectedCol,
    };
  };

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
          selectedRow={selectedRow}
          selectedCol={selectedCol}
          colScrollOffset={colScrollOffset}
          isActive={isActive}
        />
        <Box marginTop={1}>
          <Text dimColor>
            {result.rowCount} rows ({result.durationMs}ms)
            {" "}| Cell [{selectedRow + 1},{selectedCol + 1}]
            {result.rowCount > maxVisibleRows &&
              ` | Rows ${scrollOffset + 1}-${Math.min(scrollOffset + maxVisibleRows, result.rowCount)}`}
          </Text>
        </Box>
      </Box>
    );
  };

  const cellInfo = getCurrentCellInfo();

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
        {showCellDetail && cellInfo ? (
          <CellDetail
            columnName={cellInfo.columnName}
            columnType={cellInfo.columnType}
            value={cellInfo.value}
            rowIndex={cellInfo.rowIndex}
            onClose={() => setShowCellDetail(false)}
          />
        ) : (
          renderContent()
        )}
      </Box>
      {isActive && result && result.rowCount > 0 && !showCellDetail && (
        <Text dimColor>↑↓←→:navigate PgUp/Dn:page ↵:expand Home/End:col</Text>
      )}
    </Box>
  );
}
