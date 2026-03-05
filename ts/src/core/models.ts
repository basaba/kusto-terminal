export enum AuthType {
  AzureCli = "AzureCli",
  None = "None",
}

export interface KustoConnection {
  id: string;
  name: string;
  clusterUri: string;
  database: string;
  authType: AuthType;
}

export interface QueryColumn {
  name: string;
  type: string;
}

export interface QueryResult {
  success: boolean;
  columns: QueryColumn[];
  rows: Record<string, unknown>[];
  error?: string;
  durationMs: number;
  rowCount: number;
}

export interface ConnectionsConfig {
  connections: KustoConnection[];
}

export interface UserSettings {
  lastQuery?: string;
}

export type PaneId = "connections" | "query" | "results";

export interface AppState {
  activePane: PaneId;
  activeConnection: KustoConnection | null;
  isConnected: boolean;
  queryText: string;
  queryResult: QueryResult | null;
  isQueryRunning: boolean;
  statusMessage: string;
}
