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
    const colWidth = widths[i]! + (i > start ? 3 : 0);
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

  // Render each row as a single <Text> with nested <Text> children for styling.
  // This avoids flexbox layout issues that cause column misalignment when
  // cells are separate <Text> elements inside a <Box>.
  const renderHeader = () => {
    const segments: React.ReactNode[] = [];
    visibleCols.forEach((col, i) => {
      if (i > 0) segments.push(" │ ");
      const absColIdx = visColStart + i;
      const isSelectedCol = isActive && absColIdx === selectedCol;
      const cellText = truncate(col.name, visibleWidths[i]!).padEnd(visibleWidths[i]!);
      segments.push(
        <Text key={col.name} bold color={isSelectedCol ? "yellow" : "cyan"}>
          {cellText}
        </Text>,
      );
    });
    return <Text>{segments}</Text>;
  };

  const renderSeparator = () => {
    const line = visibleWidths.map((w) => "─".repeat(w)).join("─┼─");
    return <Text dimColor>{line}</Text>;
  };

  const renderRow = (row: Record<string, unknown>, absRowIdx: number) => {
    const isSelectedRow = isActive && absRowIdx === selectedRow;
    const segments: React.ReactNode[] = [];

    visibleCols.forEach((col, i) => {
      if (i > 0) {
        segments.push(
          <Text key={`sep-${i}`} dimColor={!isSelectedRow}> │ </Text>,
        );
      }
      const absColIdx = visColStart + i;
      const isSelectedCell = isSelectedRow && absColIdx === selectedCol;
      const val = formatCellValue(row[col.name]);
      const cellText = truncate(val, visibleWidths[i]!).padEnd(visibleWidths[i]!);

      if (isSelectedCell) {
        segments.push(
          <Text key={col.name} inverse bold>{cellText}</Text>,
        );
      } else {
        segments.push(
          <Text key={col.name} color={isSelectedRow ? "white" : undefined} dimColor={!isSelectedRow}>
            {cellText}
          </Text>,
        );
      }
    });

    return <Text key={absRowIdx}>{segments}</Text>;
  };

  return (
    <Box flexDirection="column">
      {renderHeader()}
      {renderSeparator()}
      {visibleRows.map((row, rowIdx) => renderRow(row, scrollOffset + rowIdx))}
    </Box>
  );
}
