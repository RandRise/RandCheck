export type IdentityConfig = { mode: string; tenantId: string; clientId: string; scope: string; api: string };
export interface IdentityClient {
  initialize(): Promise<boolean>;
  token(): Promise<string>;
  signIn(): Promise<void>;
  signOut(): Promise<void>;
}
const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export function validIdentityConfig(config: IdentityConfig) {
  return config.mode === "Entra" && guid.test(config.tenantId) && guid.test(config.clientId) &&
    /^api:\/\/[0-9a-f-]{36}\/DocumentIntelligence\.Access$/i.test(config.scope) &&
    config.tenantId !== "00000000-0000-0000-0000-000000000000" && config.clientId !== "00000000-0000-0000-0000-000000000000";
}
export async function createIdentityClient(config: IdentityConfig): Promise<IdentityClient> {
  if (!validIdentityConfig(config)) throw new Error("Sign-in configuration is incomplete");
  const { PublicClientApplication, BrowserCacheLocation } = await import("@azure/msal-browser");
  const redirectUri = `${window.location.origin}/auth/callback`;
  const client = new PublicClientApplication({
    auth: { clientId: config.clientId, authority: `https://login.microsoftonline.com/${config.tenantId}`,
      redirectUri, postLogoutRedirectUri: `${window.location.origin}/signin` },
    cache: { cacheLocation: BrowserCacheLocation.SessionStorage },
    system: { loggerOptions: { piiLoggingEnabled: false, loggerCallback: () => {} } }
  });
  await client.initialize();
  return {
    async initialize() {
      const result = await client.handleRedirectPromise({ navigateToLoginRequestUrl: false });
      const accounts = client.getAllAccounts().filter(a => a.tenantId.toLowerCase() === config.tenantId.toLowerCase());
      client.setActiveAccount(result?.account ?? (accounts.length === 1 ? accounts[0] : null));
      return !!client.getActiveAccount();
    },
    async token() {
      const account = client.getActiveAccount();
      if (!account || account.tenantId.toLowerCase() !== config.tenantId.toLowerCase()) throw new Error("Sign-in required");
      return (await client.acquireTokenSilent({ account, scopes: [config.scope] })).accessToken;
    },
    async signIn() { await client.loginRedirect({ scopes: [config.scope], prompt: "select_account" }); },
    async signOut() { await client.logoutRedirect({ account: client.getActiveAccount() }); }
  };
}
