import React, { useState, useCallback, useEffect, useRef } from "react";
import { Box, Text, useInput, useStdin } from "ink";

// F5 escape sequences: \x1b[15~ (standard) or \x1b[[E (linux console)
const F5_SEQUENCES = ["\x1b[15~", "\x1b[[E"];

interface QueryEditorProps {
  isActive: boolean;
  isQueryRunning: boolean;
  onExecute: (query: string) => void;
  onCancel: () => void;
}

export function QueryEditor({
  isActive,
  isQueryRunning,
  onExecute,
  onCancel,
}: QueryEditorProps) {
  const [lines, setLines] = useState<string[]>([""]);
  const [cursorRow, setCursorRow] = useState(0);
  const [cursorCol, setCursorCol] = useState(0);

  const linesRef = useRef(lines);
  linesRef.current = lines;

  const getFullText = useCallback(() => linesRef.current.join("\n"), []);

  // F5: execute query (raw stdin listener since Ink doesn't expose F-keys)
  const { stdin } = useStdin();
  useEffect(() => {
    if (!stdin) return;
    const handleData = (data: Buffer) => {
      const seq = data.toString();
      if (F5_SEQUENCES.includes(seq) && isActive) {
        const text = linesRef.current.join("\n").trim();
        if (text && !isQueryRunning) {
          onExecute(text);
        }
      }
    };
    stdin.on("data", handleData);
    return () => { stdin.off("data", handleData); };
  }, [stdin, isActive, isQueryRunning, onExecute]);

  useInput(
    (input, key) => {
      if (!isActive) return;

      // Ctrl+X: cancel query
      if (input === "x" && key.ctrl) {
        onCancel();
        return;
      }

      // Enter: new line
      if (key.return) {
        setLines((prev) => {
          const currentLine = prev[cursorRow] ?? "";
          const before = currentLine.slice(0, cursorCol);
          const after = currentLine.slice(cursorCol);
          const newLines = [...prev];
          newLines[cursorRow] = before;
          newLines.splice(cursorRow + 1, 0, after);
          return newLines;
        });
        setCursorRow((prev) => prev + 1);
        setCursorCol(0);
        return;
      }

      // Backspace
      if (key.backspace || key.delete) {
        if (cursorCol > 0) {
          setLines((prev) => {
            const newLines = [...prev];
            const line = newLines[cursorRow] ?? "";
            newLines[cursorRow] = line.slice(0, cursorCol - 1) + line.slice(cursorCol);
            return newLines;
          });
          setCursorCol((prev) => prev - 1);
        } else if (cursorRow > 0) {
          setLines((prev) => {
            const newLines = [...prev];
            const prevLineLen = (newLines[cursorRow - 1] ?? "").length;
            newLines[cursorRow - 1] =
              (newLines[cursorRow - 1] ?? "") + (newLines[cursorRow] ?? "");
            newLines.splice(cursorRow, 1);
            setCursorCol(prevLineLen);
            return newLines;
          });
          setCursorRow((prev) => prev - 1);
        }
        return;
      }

      // Arrow keys
      if (key.upArrow) {
        setCursorRow((prev) => {
          const newRow = Math.max(0, prev - 1);
          setCursorCol((col) => Math.min(col, (lines[newRow] ?? "").length));
          return newRow;
        });
        return;
      }
      if (key.downArrow) {
        setCursorRow((prev) => {
          const newRow = Math.min(lines.length - 1, prev + 1);
          setCursorCol((col) => Math.min(col, (lines[newRow] ?? "").length));
          return newRow;
        });
        return;
      }
      if (key.leftArrow) {
        setCursorCol((prev) => Math.max(0, prev - 1));
        return;
      }
      if (key.rightArrow) {
        setCursorCol((prev) =>
          Math.min((lines[cursorRow] ?? "").length, prev + 1),
        );
        return;
      }

      // Regular character input
      if (input && !key.ctrl && !key.meta) {
        setLines((prev) => {
          const newLines = [...prev];
          const line = newLines[cursorRow] ?? "";
          newLines[cursorRow] =
            line.slice(0, cursorCol) + input + line.slice(cursorCol);
          return newLines;
        });
        setCursorCol((prev) => prev + input.length);
      }
    },
  );

  const borderColor = isActive ? "green" : "gray";

  return (
    <Box
      flexDirection="column"
      borderStyle="round"
      borderColor={borderColor}
      paddingX={1}
      width="100%"
      height="100%"
    >
      <Box justifyContent="space-between">
        <Text bold color="white">
          Query Editor
        </Text>
        {isQueryRunning && <Text color="yellow">⏳ Running...</Text>}
      </Box>

      <Box flexDirection="column" marginTop={1} flexGrow={1}>
        {lines.map((line, rowIdx) => {
          const lineNum = String(rowIdx + 1).padStart(2, " ");
          if (isActive && rowIdx === cursorRow) {
            const before = line.slice(0, cursorCol);
            const cursor = line[cursorCol] ?? " ";
            const after = line.slice(cursorCol + 1);
            return (
              <Box key={rowIdx}>
                <Text dimColor>{lineNum} </Text>
                <Text>{before}</Text>
                <Text inverse>{cursor}</Text>
                <Text>{after}</Text>
              </Box>
            );
          }
          return (
            <Box key={rowIdx}>
              <Text dimColor>{lineNum} </Text>
              <Text>{line}</Text>
            </Box>
          );
        })}
      </Box>

      <Box marginTop={1}>
        <Text dimColor>
          F5:execute Ctrl+X:cancel
        </Text>
      </Box>
    </Box>
  );
}
