import { useCallback, useEffect, useState } from "react";
import type { AccountInfo } from "@azure/msal-browser";
import { api, type ResxEntry, type ResxFileSummary } from "./api";
import { acquireToken, initializeAuth, login, logout, ssoEnabled } from "./auth";
import { FileSidebar } from "./components/FileSidebar";
import { Header } from "./components/Header";
import { PublishPanel, type BranchMode } from "./components/PublishPanel";
import { ToastStack } from "./components/Toast";
import { TranslationEditor } from "./components/TranslationEditor";
import { PR_DEPLOYMENT_NOTICE } from "./constants";
import { useToast } from "./hooks/useToast";

type Theme = "light" | "dark";

function fileSuffix(relativePath: string | null): string | undefined {
  if (!relativePath) return undefined;
  const name = relativePath.split("/").pop() ?? relativePath;
  return name.replace(/(\.[a-z]{2}(-[A-Z]{2})?)?\.resx$/i, "");
}

export default function App() {
  const { toasts, dismiss, push } = useToast();
  const [account, setAccount] = useState<AccountInfo | null>(null);
  const [userName, setUserName] = useState("...");
  const [files, setFiles] = useState<ResxFileSummary[]>([]);
  const [selectedPath, setSelectedPath] = useState<string | null>(null);
  const [entries, setEntries] = useState<ResxEntry[]>([]);
  const [fileFilter, setFileFilter] = useState("");
  const [entryFilter, setEntryFilter] = useState("");
  const [branchMode, setBranchMode] = useState<BranchMode>("new");
  const [branchName, setBranchName] = useState("");
  const [basedOnBranch, setBasedOnBranch] = useState("main");
  const [branches, setBranches] = useState<string[]>([]);
  const [commitMessage, setCommitMessage] = useState("Türkçe RESX çevirileri güncellendi");
  const [prTitle, setPrTitle] = useState("Türkçe çeviri güncellemeleri");
  const [targetBranch, setTargetBranch] = useState("main");
  const [busy, setBusy] = useState(false);
  const [theme, setTheme] = useState<Theme>(() => {
    const savedTheme = window.localStorage.getItem("translation-ui-theme");
    return savedTheme === "dark" ? "dark" : "light";
  });

  const modeLabel = ssoEnabled ? "Kurumsal SSO" : "Development";

  useEffect(() => {
    window.localStorage.setItem("translation-ui-theme", theme);
  }, [theme]);

  const withAuth = useCallback(
    async <T,>(fn: (accessToken?: string) => Promise<T>): Promise<T> => {
      if (ssoEnabled && account) {
        return fn(await acquireToken(account));
      }
      return fn(undefined);
    },
    [account]
  );

  const refreshBranchDefaults = useCallback(
    async (suffix?: string) => {
      try {
        const defaults = await withAuth((t) => api.branchDefaults(suffix, t));
        setBranchName(defaults.suggestedBranchName);
        setTargetBranch(defaults.targetBranch);
        setBasedOnBranch(defaults.targetBranch);
        setBranches(defaults.branches);
      } catch (e) {
        push("error", e instanceof Error ? e.message : String(e));
      }
    },
    [withAuth, push]
  );

  const loadFiles = useCallback(async () => {
    setBusy(true);
    try {
      setFiles(await withAuth((t) => api.listResx(t)));
    } catch (e) {
      push("error", e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  }, [withAuth, push]);

  const loadFile = useCallback(
    async (relativePath: string) => {
      setBusy(true);
      try {
        const detail = await withAuth((t) => api.getResx(relativePath, t));
        setSelectedPath(detail.relativePath);
        setEntries(detail.entries);
        setEntryFilter("");
        await refreshBranchDefaults(fileSuffix(detail.relativePath));
      } catch (e) {
        push("error", e instanceof Error ? e.message : String(e));
      } finally {
        setBusy(false);
      }
    },
    [withAuth, push, refreshBranchDefaults]
  );

  useEffect(() => {
    void (async () => {
      const acc = await initializeAuth();
      setAccount(acc);
      const token = acc ? await acquireToken(acc) : undefined;
      const me = await api.me(token);
      setUserName(me.name ?? "Geliştirici");
      await refreshBranchDefaults();
      await loadFiles();
    })();
  }, [loadFiles, refreshBranchDefaults]);

  async function handleLogin() {
    const acc = await login();
    setAccount(acc);
    const me = await api.me(await acquireToken(acc));
    setUserName(me.name ?? acc.username);
    await refreshBranchDefaults(fileSuffix(selectedPath));
    push("success", "Giriş başarılı");
  }

  async function handleLogout() {
    await logout();
    setAccount(null);
    setUserName("Geliştirici");
  }

  async function handlePull() {
    setBusy(true);
    try {
      const result = await withAuth((t) => api.pull(t));
      push("success", result.message);
      await loadFiles();
      await refreshBranchDefaults(fileSuffix(selectedPath));
    } catch (e) {
      push("error", e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  }

  async function handleSuggestBranch() {
    await refreshBranchDefaults(fileSuffix(selectedPath));
    push("success", "Branch adı güncellendi");
  }

  async function handleCreateBranch() {
    if (!branchName.trim()) return;
    setBusy(true);
    try {
      const result = await withAuth((t) =>
        api.createBranch({ branchName: branchName.trim(), fromBranch: basedOnBranch }, t)
      );
      push("success", result.message);
      const list = await withAuth((t) => api.listBranches(t));
      setBranches(list.branches);
    } catch (e) {
      push("error", e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  }

  async function handleSave() {
    if (!selectedPath) return;
    setBusy(true);
    try {
      const result = await withAuth((t) => api.saveResx(selectedPath, entries, t));
      push("success", `${result.relativePath} — ${result.updatedCount} değişiklik kaydedildi`);
      await loadFiles();
    } catch (e) {
      push("error", e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  }

  async function handlePublish() {
    if (!selectedPath || !branchName.trim()) return;
    setBusy(true);
    try {
      await withAuth((t) => api.saveResx(selectedPath, entries, t));

      if (branchMode === "new") {
        await withAuth((t) =>
          api.createBranch({ branchName: branchName.trim(), fromBranch: basedOnBranch }, t)
        );
      }

      await withAuth((t) =>
        api.commitPush(
          { branchName: branchName.trim(), commitMessage, resxRelativePaths: [selectedPath] },
          t
        )
      );
      const pr = await withAuth((t) =>
        api.createPullRequest(
          { sourceBranch: branchName.trim(), targetBranch, title: prTitle, description: commitMessage },
          t
        )
      );
      push("success", `PR #${pr.pullRequestId} oluşturuldu. ${PR_DEPLOYMENT_NOTICE}`);
      window.open(pr.url, "_blank", "noopener,noreferrer");
    } catch (e) {
      push("error", e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="app-shell" data-theme={theme}>
      <div className="ambient" aria-hidden />
      <Header
        userName={userName}
        ssoEnabled={ssoEnabled}
        isSignedIn={Boolean(account)}
        busy={busy}
        modeLabel={modeLabel}
        theme={theme}
        onThemeChange={setTheme}
        onSignIn={() => void handleLogin()}
        onSignOut={() => void handleLogout()}
        onPull={() => void handlePull()}
      />

      <div className="workspace">
        <FileSidebar
          files={files}
          selectedPath={selectedPath}
          fileFilter={fileFilter}
          busy={busy}
          onFileFilterChange={setFileFilter}
          onSelect={(path) => void loadFile(path)}
        />
        <TranslationEditor
          selectedPath={selectedPath}
          entries={entries}
          filter={entryFilter}
          busy={busy}
          onFilterChange={setEntryFilter}
          onSave={() => void handleSave()}
          onEntryChange={(name, value) =>
            setEntries((prev) => prev.map((e) => (e.name === name ? { ...e, value } : e)))
          }
        />
        <PublishPanel
          branchMode={branchMode}
          branchName={branchName}
          branches={branches}
          basedOnBranch={basedOnBranch}
          commitMessage={commitMessage}
          prTitle={prTitle}
          targetBranch={targetBranch}
          busy={busy}
          disabled={!selectedPath}
          onBranchModeChange={setBranchMode}
          onBranchChange={setBranchName}
          onBasedOnBranchChange={setBasedOnBranch}
          onSuggestBranch={() => void handleSuggestBranch()}
          onCreateBranch={() => void handleCreateBranch()}
          onCommitMessageChange={setCommitMessage}
          onPrTitleChange={setPrTitle}
          onTargetBranchChange={setTargetBranch}
          onPublish={() => void handlePublish()}
        />
      </div>

      {busy ? <div className="global-busy" aria-hidden /> : null}
      <ToastStack toasts={toasts} onDismiss={dismiss} />
    </div>
  );
}
