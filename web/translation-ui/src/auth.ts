import { PublicClientApplication, type AccountInfo } from "@azure/msal-browser";

const tenantId = import.meta.env.VITE_AZURE_AD_TENANT_ID as string | undefined;
const clientId = import.meta.env.VITE_AZURE_AD_CLIENT_ID as string | undefined;
const apiScope = import.meta.env.VITE_AZURE_AD_API_SCOPE as string | undefined;

export const ssoEnabled = Boolean(tenantId && clientId && apiScope);

let msal: PublicClientApplication | null = null;

function getMsal(): PublicClientApplication {
  if (!ssoEnabled) throw new Error("SSO is not configured.");
  if (!msal) {
    msal = new PublicClientApplication({
      auth: {
        clientId: clientId!,
        authority: `https://login.microsoftonline.com/${tenantId}`,
        redirectUri: window.location.origin
      },
      cache: { cacheLocation: "sessionStorage" }
    });
  }
  return msal;
}

export async function initializeAuth(): Promise<AccountInfo | null> {
  if (!ssoEnabled) return null;
  const app = getMsal();
  await app.initialize();
  const result = await app.handleRedirectPromise();
  if (result?.account) return result.account;
  const accounts = app.getAllAccounts();
  return accounts[0] ?? null;
}

export async function login(): Promise<AccountInfo> {
  const app = getMsal();
  const result = await app.loginPopup({ scopes: [apiScope!] });
  if (!result.account) throw new Error("Login failed.");
  return result.account;
}

export async function logout(): Promise<void> {
  const app = getMsal();
  const account = app.getAllAccounts()[0];
  if (account) await app.logoutPopup({ account });
}

export async function acquireToken(account: AccountInfo): Promise<string> {
  const app = getMsal();
  try {
    const silent = await app.acquireTokenSilent({ account, scopes: [apiScope!] });
    return silent.accessToken;
  } catch {
    const interactive = await app.acquireTokenPopup({ account, scopes: [apiScope!] });
    return interactive.accessToken;
  }
}
