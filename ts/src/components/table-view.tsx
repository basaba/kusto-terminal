import React from "react";
import { Box, Text } from "ink";
import type { QueryResult, QueryColumn } from "../core/models.js";

interface TableViewProps {
  result: QueryResult;
  scrollOffset: number;
  maxVisibleRows: number;
  selectedRow: number;
  selectedCol: number;
  colScrollOffset: number;
  isActive: boolean;
}

function truncate(str: string, maxLen: number): string {
  if (str.length <= maxLen) return str;
  return str.slice(0, maxLen - 1) + "…";
}

export function formatCellValue(value: unknown): string {
  if (value === null || value === undefined) return "null";
  if (value instanceof Date) return value.toISOString();
  if (typeof value === "object") return JSON.stringify(value);
  return String(value);
}

function computeColumnWidths(
  columns: QueryColumn[],
  rows: Record<string, unknown>[],
  maxWidth: number,
): number[] {
  const widths = columns.map((col) => col.name.length);

  const sampleRows = rows.slice(0, 100);
  for (const row of sampleRows) {
    for (let i = 0; i < columns.length; i++) {
      const val = formatCellValue(row[columns[i]!.name]);
      widths[i] = Math.max(widths[i]!, Math.min(val.length, 40));
    }
  }

  const totalWidth = widths.reduce((a, b) => a + b, 0) + (columns.length - 1) * 3;
  if (totalWidth > maxWidth && columns.length > 0) {
    const scale = maxWidth / totalWidth;
    for (let i = 0; i < widths.length; i++) {
      widths[i] = Math.max(4, Math.floor(widths[i]! * scale));
    }
  }

  return widths;
}

function getVisibleColumns(
  widths: number[],
  colScrollOffset: number,
  maxWidth: number,
): { start: number; end: number } {
  let usedWidth = 0;
  const start = colScrollOffset;
  let end = start;
  for (let i = start; i < widths.length; i++) {
    const colWidth = widths[i]! + (i > start ? 3 : 0); // 3 for " │ " separator
    if (usedWidth + colWidth > maxWidth && i > start) break;
    usedWidth += colWidth;
    end = i + 1;
  }
  return { start, end };
}

export function TableView({
  result,
  scrollOffset,
  maxVisibleRows,
  selectedRow,
  selectedCol,
  colScrollOffset,
  isActive,
}: TableViewProps) {
  const { columns, rows } = result;

  if (columns.length === 0) {
    return <Text dimColor>No columns in result</Text>;
  }

  const maxWidth = process.stdout.columns ? process.stdout.columns - 10 : 120;
  const widths = computeColumnWidths(columns, rows, maxWidth);
  const { start: visColStart, end: visColEnd } = getVisibleColumns(
    widths,
    colScrollOffset,
    maxWidth,
  );

  const visibleCols = columns.slice(visColStart, visColEnd);
  const visibleWidths = widths.slice(visColStart, visColEnd);
  const visibleRows = rows.slice(scrollOffset, scrollOffset + maxVisibleRows);

  return (
    <Box flexDirection="column">
      {/* Column headers */}
      <Box>
        {visibleCols.map((col, i) => {
          const absColIdx = visColStart + i;
          const isSelectedCol = isActive && absColIdx === selectedCol;
          const cellText = truncate(col.name, visibleWidths[i]!).padEnd(visibleWidths[i]!);
          const sep = i < visibleCols.length - 1 ? " │ " : "";
          return (
            <Text key={col.name} bold color={isSelectedCol ? "yellow" : "cyan"}>
              {cellText}{sep}
            </Text>
          );
        })}
      </Box>

      {/* Separator */}
      <Text dimColor>
        {visibleWidths.map((w) => "─".repeat(w)).join("─┼─")}
      </Text>

      {/* Data rows */}
      {visibleRows.map((row, rowIdx) => {
        const absRowIdx = scrollOffset + rowIdx;
        const isSelectedRow = isActive && absRowIdx === selectedRow;

        return (
          <Box key={absRowIdx}>
            {visibleCols.map((col, i) => {
              const absColIdx = visColStart + i;
              const isSelectedCell =
                isSelectedRow && absColIdx === selectedCol;
              const val = formatCellValue(row[col.name]);
              const cellText = truncate(val, visibleWidths[i]!).padEnd(visibleWidths[i]!);
              const sep = i < visibleCols.length - 1 ? " │ " : "";

              if (isSelectedCell) {
                return (
                  <React.Fragment key={col.name}>
                    <Text inverse bold>{cellText}</Text>
                    <Text>{sep}</Text>
                  </React.Fragment>
                );
              }

              return (
                <Text
                  key={col.name}
                  color={isSelectedRow ? "white" : undefined}
                  dimColor={!isSelectedRow}
                >
                  {cellText}{sep}
                </Text>
              );
            })}
          </Box>
        );
      })}
    </Box>
  );
}
