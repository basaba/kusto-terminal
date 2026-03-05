import { useState, useCallback, useEffect } from "react";
import type { KustoConnection } from "../core/models.js";
import {
  loadConnections,
  saveConnection,
  deleteConnection as deleteConn,
  generateConnectionId,
} from "../core/connection-manager.js";
import { AuthType } from "../core/models.js";

export function useConnections() {
  const [connections, setConnections] = useState<KustoConnection[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    loadConnections()
      .then(setConnections)
      .finally(() => setIsLoading(false));
  }, []);

  const addConnection = useCallback(
    async (partial: Omit<KustoConnection, "id">) => {
      const connection: KustoConnection = {
        ...partial,
        id: generateConnectionId(),
      };
      await saveConnection(connection);
      setConnections((prev) => [...prev, connection]);
      return connection;
    },
    [],
  );

  const updateConnection = useCallback(async (connection: KustoConnection) => {
    await saveConnection(connection);
    setConnections((prev) =>
      prev.map((c) => (c.id === connection.id ? connection : c)),
    );
  }, []);

  const removeConnection = useCallback(async (id: string) => {
    await deleteConn(id);
    setConnections((prev) => prev.filter((c) => c.id !== id));
  }, []);

  const addQuickConnection = useCallback(
    async (clusterUri: string, database: string) => {
      return addConnection({
        name: new URL(clusterUri).hostname.split(".")[0] ?? clusterUri,
        clusterUri,
        database,
        authType: AuthType.AzureCli,
      });
    },
    [addConnection],
  );

  return {
    connections,
    isLoading,
    addConnection,
    updateConnection,
    removeConnection,
    addQuickConnection,
  };
}
