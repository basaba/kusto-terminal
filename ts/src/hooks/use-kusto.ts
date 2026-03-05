import { useState, useCallback, useRef } from "react";
import { KustoClient } from "../core/kusto-client.js";
import type { KustoConnection, QueryResult } from "../core/models.js";

export function useKusto() {
  const [isConnected, setIsConnected] = useState(false);
  const [isQueryRunning, setIsQueryRunning] = useState(false);
  const [queryResult, setQueryResult] = useState<QueryResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const clientRef = useRef(new KustoClient());

  const connect = useCallback(async (connection: KustoConnection) => {
    setError(null);
    try {
      await clientRef.current.connect(connection);
      setIsConnected(true);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Connection failed";
      setError(msg);
      setIsConnected(false);
    }
  }, []);

  const executeQuery = useCallback(async (query: string) => {
    if (!clientRef.current.isConnected()) {
      setError("Not connected");
      return;
    }

    setIsQueryRunning(true);
    setError(null);
    setQueryResult(null);

    try {
      const result = await clientRef.current.executeQuery(query);
      setQueryResult(result);
      if (!result.success) {
        setError(result.error ?? "Query failed");
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Query execution failed";
      setError(msg);
    } finally {
      setIsQueryRunning(false);
    }
  }, []);

  const disconnect = useCallback(() => {
    clientRef.current.disconnect();
    setIsConnected(false);
    setQueryResult(null);
    setError(null);
  }, []);

  const listDatabases = useCallback(async (): Promise<string[]> => {
    return clientRef.current.listDatabases();
  }, []);

  return {
    isConnected,
    isQueryRunning,
    queryResult,
    error,
    connect,
    executeQuery,
    disconnect,
    listDatabases,
    getConnection: () => clientRef.current.getConnection(),
  };
}
