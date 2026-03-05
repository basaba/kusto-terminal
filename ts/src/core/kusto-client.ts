import { Client, KustoConnectionStringBuilder } from "azure-kusto-data";
import type { KustoResponseDataSet } from "azure-kusto-data";
import { getToken } from "./auth.js";
import type { KustoConnection, QueryResult, QueryColumn } from "./models.js";
import { AuthType } from "./models.js";

export class KustoClient {
  private client: Client | null = null;
  private connection: KustoConnection | null = null;

  async connect(connection: KustoConnection): Promise<void> {
    let kcsb: ReturnType<typeof KustoConnectionStringBuilder.withTokenProvider>;

    if (connection.authType === AuthType.AzureCli) {
      kcsb = KustoConnectionStringBuilder.withTokenProvider(
        connection.clusterUri,
        getToken,
      );
    } else {
      kcsb = KustoConnectionStringBuilder.withTokenProvider(
        connection.clusterUri,
        async () => "",
      );
    }

    this.client = new Client(kcsb);
    this.connection = connection;
  }

  isConnected(): boolean {
    return this.client !== null;
  }

  getConnection(): KustoConnection | null {
    return this.connection;
  }

  async executeQuery(query: string): Promise<QueryResult> {
    if (!this.client || !this.connection) {
      return {
        success: false,
        columns: [],
        rows: [],
        error: "Not connected to any cluster",
        durationMs: 0,
        rowCount: 0,
      };
    }

    const startTime = Date.now();
    try {
      const isCommand = query.trimStart().startsWith(".");
      let response: KustoResponseDataSet;

      if (isCommand) {
        response = await this.client.executeMgmt(
          this.connection.database,
          query,
        );
      } else {
        response = await this.client.execute(this.connection.database, query);
      }

      const durationMs = Date.now() - startTime;
      const primaryResults = response.primaryResults[0];

      if (!primaryResults) {
        return {
          success: true,
          columns: [],
          rows: [],
          durationMs,
          rowCount: 0,
        };
      }

      const columns: QueryColumn[] = primaryResults.columns.map((col) => ({
        name: col.name ?? "",
        type: col.type ?? "string",
      }));

      const rows: Record<string, unknown>[] = [];
      for (const row of primaryResults.rows()) {
        const record: Record<string, unknown> = {};
        for (const col of columns) {
          record[col.name] = row.getValueAt(
            columns.findIndex((c) => c.name === col.name),
          );
        }
        rows.push(record);
      }

      return {
        success: true,
        columns,
        rows,
        durationMs,
        rowCount: rows.length,
      };
    } catch (err: unknown) {
      const durationMs = Date.now() - startTime;
      const message =
        err instanceof Error ? err.message : "Unknown error occurred";
      return {
        success: false,
        columns: [],
        rows: [],
        error: message,
        durationMs,
        rowCount: 0,
      };
    }
  }

  async listDatabases(): Promise<string[]> {
    const result = await this.executeQuery(".show databases");
    if (!result.success) return [];
    return result.rows
      .map((row) => row["DatabaseName"] as string)
      .filter(Boolean);
  }

  disconnect(): void {
    this.client = null;
    this.connection = null;
  }
}
