import fs from "node:fs/promises";
import path from "node:path";
import os from "node:os";
import type { KustoConnection, ConnectionsConfig } from "./models.js";

const CONFIG_DIR = path.join(os.homedir(), ".kusto-terminal");
const CONNECTIONS_FILE = path.join(CONFIG_DIR, "connections.json");

async function ensureConfigDir(): Promise<void> {
  await fs.mkdir(CONFIG_DIR, { recursive: true });
}

async function readConfig(): Promise<ConnectionsConfig> {
  try {
    const data = await fs.readFile(CONNECTIONS_FILE, "utf-8");
    return JSON.parse(data) as ConnectionsConfig;
  } catch {
    return { connections: [] };
  }
}

async function writeConfig(config: ConnectionsConfig): Promise<void> {
  await ensureConfigDir();
  await fs.writeFile(CONNECTIONS_FILE, JSON.stringify(config, null, 2), "utf-8");
}

export async function loadConnections(): Promise<KustoConnection[]> {
  const config = await readConfig();
  return config.connections;
}

export async function saveConnection(connection: KustoConnection): Promise<void> {
  const config = await readConfig();
  const index = config.connections.findIndex((c) => c.id === connection.id);
  if (index >= 0) {
    config.connections[index] = connection;
  } else {
    config.connections.push(connection);
  }
  await writeConfig(config);
}

export async function deleteConnection(id: string): Promise<void> {
  const config = await readConfig();
  config.connections = config.connections.filter((c) => c.id !== id);
  await writeConfig(config);
}

export function validateConnection(conn: Partial<KustoConnection>): string | null {
  if (!conn.clusterUri?.trim()) return "Cluster URI is required";
  if (!conn.database?.trim()) return "Database is required";
  try {
    new URL(conn.clusterUri);
  } catch {
    return "Invalid cluster URI";
  }
  return null;
}

export function generateConnectionId(): string {
  return `conn-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}
