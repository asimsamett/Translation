import type { ResxFileSummary } from "../api";
import { IconFile, IconSearch } from "./Icons";

type FileSidebarProps = {
  files: ResxFileSummary[];
  selectedPath: string | null;
  fileFilter: string;
  busy: boolean;
  onFileFilterChange: (value: string) => void;
  onSelect: (path: string) => void;
};

export function FileSidebar({
  files,
  selectedPath,
  fileFilter,
  busy,
  onFileFilterChange,
  onSelect
}: FileSidebarProps) {
  const q = fileFilter.trim().toLowerCase();
  const filtered = q
    ? files.filter((f) => f.relativePath.toLowerCase().includes(q))
    : files;

  return (
    <aside className="panel files-panel">
      <div className="panel-head">
        <h2>Kaynak dosyalar</h2>
        <span className="count">{filtered.length}</span>
      </div>

      <div className="search-field">
        <IconSearch className="search-icon" />
        <input
          type="search"
          placeholder="RESX dosyası ara..."
          value={fileFilter}
          onChange={(e) => onFileFilterChange(e.target.value)}
        />
      </div>

      <ul className="file-list">
        {filtered.length === 0 ? (
          <li className="file-empty">Dosya bulunamadı. Önce repodan çekin.</li>
        ) : (
          filtered.map((f) => (
            <li key={f.relativePath}>
              <button
                type="button"
                className={`file-item${selectedPath === f.relativePath ? " active" : ""}`}
                disabled={busy}
                onClick={() => onSelect(f.relativePath)}
              >
                <IconFile className="file-icon" />
                <span className="file-meta">
                  <strong>
                    {f.fileName}
                    <span className="culture-badge">{f.culture ?? "varsayılan"}</span>
                  </strong>
                  <small>{f.relativePath}</small>
                  <em>{f.entryCount} anahtar</em>
                </span>
              </button>
            </li>
          ))
        )}
      </ul>
    </aside>
  );
}
