import { IconRocket } from "./Icons";

export type BranchMode = "new" | "existing";

type PublishPanelProps = {
  branchMode: BranchMode;
  branchName: string;
  branches: string[];
  basedOnBranch: string;
  commitMessage: string;
  prTitle: string;
  targetBranch: string;
  busy: boolean;
  disabled: boolean;
  onBranchModeChange: (mode: BranchMode) => void;
  onBranchChange: (v: string) => void;
  onBasedOnBranchChange: (v: string) => void;
  onSuggestBranch: () => void;
  onCreateBranch: () => void;
  onCommitMessageChange: (v: string) => void;
  onPrTitleChange: (v: string) => void;
  onTargetBranchChange: (v: string) => void;
  onPublish: () => void;
};

export function PublishPanel({
  branchMode,
  branchName,
  branches,
  basedOnBranch,
  commitMessage,
  prTitle,
  targetBranch,
  busy,
  disabled,
  onBranchModeChange,
  onBranchChange,
  onBasedOnBranchChange,
  onSuggestBranch,
  onCreateBranch,
  onCommitMessageChange,
  onPrTitleChange,
  onTargetBranchChange,
  onPublish
}: PublishPanelProps) {
  return (
    <aside className="panel publish-panel">
      <div className="panel-head">
        <h2>Yayınla</h2>
        <p>Branch · commit · push · PR</p>
      </div>

      <form
        className="publish-form"
        onSubmit={(e) => {
          e.preventDefault();
          onPublish();
        }}
      >
        <fieldset className="branch-mode">
          <legend>Branch seçimi</legend>
          <label className="radio">
            <input
              type="radio"
              name="branchMode"
              checked={branchMode === "new"}
              onChange={() => onBranchModeChange("new")}
            />
            Yeni branch
          </label>
          <label className="radio">
            <input
              type="radio"
              name="branchMode"
              checked={branchMode === "existing"}
              onChange={() => onBranchModeChange("existing")}
            />
            Mevcut branch
          </label>
        </fieldset>

        {branchMode === "existing" ? (
          <label>
            <span>Branch listesi</span>
            <select value={branchName} onChange={(e) => onBranchChange(e.target.value)}>
              <option value="">Seçin...</option>
              {branches.map((b) => (
                <option key={b} value={b}>
                  {b}
                </option>
              ))}
            </select>
          </label>
        ) : (
          <>
            <label>
              <span>Temel alınacak branch</span>
              <input value={basedOnBranch} onChange={(e) => onBasedOnBranchChange(e.target.value)} />
            </label>
            <label>
              <span>Yeni branch adı</span>
              <div className="input-row">
                <input value={branchName} onChange={(e) => onBranchChange(e.target.value)} />
                <button type="button" className="btn btn-ghost btn-sm" disabled={busy} onClick={onSuggestBranch}>
                  Öner
                </button>
              </div>
            </label>
            <button
              type="button"
              className="btn btn-secondary btn-block"
              disabled={busy || !branchName}
              onClick={onCreateBranch}
            >
              Branch oluştur (lokal)
            </button>
          </>
        )}

        <label>
          <span>Commit mesajı</span>
          <input value={commitMessage} onChange={(e) => onCommitMessageChange(e.target.value)} />
        </label>
        <label>
          <span>PR başlığı</span>
          <input value={prTitle} onChange={(e) => onPrTitleChange(e.target.value)} />
        </label>
        <label>
          <span>PR hedef branch</span>
          <input value={targetBranch} onChange={(e) => onTargetBranchChange(e.target.value)} />
        </label>

        <button type="submit" className="btn btn-accent btn-block" disabled={busy || disabled || !branchName}>
          <IconRocket />
          PR oluştur
        </button>
      </form>

      <p className="publish-hint">
        Yeni branch modunda isim şablondan üretilir. PR oluştururken branch yoksa otomatik{' '}
        <code>{basedOnBranch || targetBranch}</code> üzerinden oluşturulur.
      </p>
    </aside>
  );
}
