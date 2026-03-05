import React from "react";
import { Box, Text } from "ink";
import type { QueryResult, QueryColumn } from "../core/models.js";

interface TableViewProps {
  result: QueryResult;
  scrollOffset: number;
  maxVisibleRows: number;
}

function truncate(str: string, maxLen: number): string {
  if (str.length <= maxLen) return str;
  return str.slice(0, maxLen - 1) + "…";
}

function formatCellValue(value: unknown): string {
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

export function TableView({ result, scrollOffset, maxVisibleRows }: TableViewProps) {
  const { columns, rows } = result;

  if (columns.length === 0) {
    return <Text dimColor>No columns in result</Text>;
  }

  const maxWidth = process.stdout.columns ? process.stdout.columns - 10 : 120;
  const widths = computeColumnWidths(columns, rows, maxWidth);

  const headerCells = columns.map((col, i) =>
    truncate(col.name, widths[i]!).padEnd(widths[i]!),
  );
  const separator = widths.map((w) => "─".repeat(w));

  const visibleRows = rows.slice(scrollOffset, scrollOffset + maxVisibleRows);

  return (
    <Box flexDirection="column">
      <Text bold color="cyan">
        {headerCells.join(" │ ")}
      </Text>
      <Text dimColor>{separator.join("─┼─")}</Text>
      {visibleRows.map((row, rowIdx) => {
        const cells = columns.map((col, i) => {
          const val = formatCellValue(row[col.name]);
          return truncate(val, widths[i]!).padEnd(widths[i]!);
        });
        return (
          <Text key={scrollOffset + rowIdx}>{cells.join(" │ ")}</Text>
        );
      })}
    </Box>
  );
}
