export type ResxFileSummary = {
  relativePath: string;
  fileName: string;
  culture?: string | null;
  entryCount: number;
  lastModifiedUtc: string;
};

export type ResxEntry = {
  name: string;
  value: string;
  comment?: string | null;
};

export type ResxFileDetail = {
  relativePath: string;
  entries: ResxEntry[];
};

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, "") ?? "";

async function request<T>(path: string, init?: RequestInit, token?: string): Promise<T> {
  const headers = new Headers(init?.headers);
  headers.set("Content-Type", "application/json");
  if (token) headers.set("Authorization", `Bearer ${token}`);

  const response = await fetch(`${apiBaseUrl}${path}`, { ...init, headers, credentials: "include" });
  if (!response.ok) {
    throw new Error(await readErrorMessage(response));
  }
  return response.json() as Promise<T>;
}

async function readErrorMessage(response: Response): Promise<string> {
  const contentType = response.headers.get("Content-Type") ?? "";
  const fallback = `${response.status} ${response.statusText}`.trim();

  if (contentType.includes("application/json") || contentType.includes("application/problem+json")) {
    const body = (await response.json().catch(() => null)) as
      | { title?: string; detail?: string; message?: string }
      | null;
    return body?.detail || body?.message || body?.title || fallback;
  }

  const text = await response.text();
  return simplifyServerError(text) || fallback;
}

function simplifyServerError(text: string): string {
  const withoutHtml = text
    .replace(/<style[\s\S]*?<\/style>/gi, " ")
    .replace(/<script[\s\S]*?<\/script>/gi, " ")
    .replace(/<[^>]+>/g, " ")
    .replace(/\s+/g, " ")
    .trim();

  const match = withoutHtml.match(/System\.[A-Za-z.]*Exception:\s*(.*?)(?:\s+at\s+|\s+--- End|\s*$)/);
  return (match?.[1] ?? withoutHtml).slice(0, 1200);
}

export const api = {
  me: (token?: string) => request<{ name?: string; isAuthenticated: boolean }>("/api/me", undefined, token),

  listResx: (token?: string) =>
    request<ResxFileSummary[]>("/api/resx", undefined, token),

  getResx: (relativePath: string, token?: string) =>
    request<ResxFileDetail>(`/api/resx/${encodeURIComponent(relativePath)}`, undefined, token),

  saveResx: (relativePath: string, entries: ResxEntry[], token?: string) =>
    request<{ relativePath: string; updatedCount: number }>(
      `/api/resx/${encodeURIComponent(relativePath)}`,
      { method: "PUT", body: JSON.stringify({ entries }) },
      token
    ),

  pull: (token?: string) =>
    request<{ branch: string; message: string }>("/api/git/pull", { method: "POST" }, token),

  commitPush: (
    body: { branchName: string; commitMessage: string; resxRelativePaths: string[] },
    token?: string
  ) =>
    request<{ branch: string; commitSha: string; filesCommitted: number }>(
      "/api/git/commit-push",
      { method: "POST", body: JSON.stringify(body) },
      token
    ),

  createPullRequest: (
    body: { sourceBranch: string; targetBranch: string; title: string; description?: string },
    token?: string
  ) =>
    request<{ pullRequestId: number; url: string; title: string }>(
      "/api/pull-requests",
      { method: "POST", body: JSON.stringify(body) },
      token
    ),

  prDefaults: (token?: string) =>
    request<{ targetBranch: string }>("/api/pull-requests/defaults", undefined, token),

  listBranches: (token?: string) =>
    request<{ branches: string[]; currentBranch?: string | null }>("/api/git/branches", undefined, token),

  branchDefaults: (suffix?: string, token?: string) => {
    const q = suffix ? `?suffix=${encodeURIComponent(suffix)}` : "";
    return request<{
      suggestedBranchName: string;
      targetBranch: string;
      currentBranch?: string | null;
      branches: string[];
    }>(`/api/git/branch-defaults${q}`, undefined, token);
  },

  createBranch: (body: { branchName: string; fromBranch?: string }, token?: string) =>
    request<{ branchName: string; basedOn: string; created: boolean; message: string }>(
      "/api/git/branches",
      { method: "POST", body: JSON.stringify(body) },
      token
    )
};
