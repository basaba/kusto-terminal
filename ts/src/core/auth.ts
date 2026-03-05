import { AzureCliCredential } from "@azure/identity";

const KUSTO_SCOPE = "https://kusto.kusto.windows.net/.default";

let credential: AzureCliCredential | null = null;

function getCredential(): AzureCliCredential {
  if (!credential) {
    credential = new AzureCliCredential();
  }
  return credential;
}

export async function getToken(): Promise<string> {
  const cred = getCredential();
  const tokenResponse = await cred.getToken(KUSTO_SCOPE);
  return tokenResponse.token;
}

export async function validateAzureCli(): Promise<boolean> {
  try {
    await getToken();
    return true;
  } catch {
    return false;
  }
}
