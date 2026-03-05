import React, { useState } from "react";
import { Box, Text, useInput } from "ink";
import type { KustoConnection, PaneId } from "../core/models.js";
import { AuthType } from "../core/models.js";

interface ConnectionPaneProps {
  connections: KustoConnection[];
  activeConnection: KustoConnection | null;
  isActive: boolean;
  isAddingMode: boolean;
  onConnect: (connection: KustoConnection) => void;
  onDelete: (id: string) => void;
  onAddStart: () => void;
  onAddComplete: (clusterUri: string, database: string) => void;
  onAddCancel: () => void;
}

type AddStep = "cluster" | "database";

export function ConnectionPane({
  connections,
  activeConnection,
  isActive,
  isAddingMode,
  onConnect,
  onDelete,
  onAddStart,
  onAddComplete,
  onAddCancel,
}: ConnectionPaneProps) {
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [addStep, setAddStep] = useState<AddStep>("cluster");
  const [clusterInput, setClusterInput] = useState("");
  const [databaseInput, setDatabaseInput] = useState("");

  useInput(
    (input, key) => {
      if (!isActive) return;

      if (isAddingMode) {
        if (key.escape) {
          onAddCancel();
          setAddStep("cluster");
          setClusterInput("");
          setDatabaseInput("");
          return;
        }

        if (key.return) {
          if (addStep === "cluster" && clusterInput.trim()) {
            setAddStep("database");
          } else if (addStep === "database" && databaseInput.trim()) {
            onAddComplete(clusterInput.trim(), databaseInput.trim());
            setAddStep("cluster");
            setClusterInput("");
            setDatabaseInput("");
          }
          return;
        }

        if (key.backspace || key.delete) {
          if (addStep === "cluster") {
            setClusterInput((prev) => prev.slice(0, -1));
          } else {
            setDatabaseInput((prev) => prev.slice(0, -1));
          }
          return;
        }

        if (input && !key.ctrl && !key.meta) {
          if (addStep === "cluster") {
            setClusterInput((prev) => prev + input);
          } else {
            setDatabaseInput((prev) => prev + input);
          }
        }
        return;
      }

      if (key.upArrow) {
        setSelectedIndex((prev) => Math.max(0, prev - 1));
      } else if (key.downArrow) {
        setSelectedIndex((prev) =>
          Math.min(connections.length - 1, prev + 1),
        );
      } else if (key.return && connections.length > 0) {
        const conn = connections[selectedIndex];
        if (conn) onConnect(conn);
      } else if (input === "d" && connections.length > 0) {
        const conn = connections[selectedIndex];
        if (conn) onDelete(conn.id);
        setSelectedIndex((prev) => Math.max(0, prev - 1));
      } else if (input === "n") {
        onAddStart();
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
      <Text bold color="white">
        Connections
      </Text>

      {isAddingMode ? (
        <Box flexDirection="column" marginTop={1}>
          <Text color="yellow">New Connection</Text>
          <Box marginTop={1}>
            <Text>Cluster: </Text>
            <Text color={addStep === "cluster" ? "green" : "white"}>
              {clusterInput}
              {addStep === "cluster" ? "█" : ""}
            </Text>
          </Box>
          {addStep === "database" && (
            <Box>
              <Text>Database: </Text>
              <Text color="green">
                {databaseInput}█
              </Text>
            </Box>
          )}
          <Text dimColor>
            Enter to confirm, Esc to cancel
          </Text>
        </Box>
      ) : (
        <Box flexDirection="column" marginTop={1}>
          {connections.length === 0 ? (
            <Text dimColor>No connections. Press 'n' to add.</Text>
          ) : (
            connections.map((conn, idx) => {
              const isSelected = idx === selectedIndex;
              const isConnected = activeConnection?.id === conn.id;
              const prefix = isSelected ? "▸ " : "  ";
              const suffix = isConnected ? " ●" : "";

              return (
                <Text
                  key={conn.id}
                  color={isConnected ? "green" : isSelected ? "yellow" : "white"}
                  bold={isSelected}
                >
                  {prefix}
                  {conn.name}
                  {suffix}
                </Text>
              );
            })
          )}
          <Box marginTop={1}>
            <Text dimColor>
              n:add ↵:connect d:delete
            </Text>
          </Box>
        </Box>
      )}
    </Box>
  );
}
