import type { ResxEntry } from "../api";
import { IconSave, IconSearch } from "./Icons";

type TranslationEditorProps = {
  selectedPath: string | null;
  entries: ResxEntry[];
  filter: string;
  busy: boolean;
  onFilterChange: (value: string) => void;
  onSave: () => void;
  onEntryChange: (name: string, value: string) => void;
};

export function TranslationEditor({
  selectedPath,
  entries,
  filter,
  busy,
  onFilterChange,
  onSave,
  onEntryChange
}: TranslationEditorProps) {
  const q = filter.trim().toLowerCase();
  const filtered = q
    ? entries.filter(
        (e) => e.name.toLowerCase().includes(q) || e.value.toLowerCase().includes(q)
      )
    : entries;

  if (!selectedPath) {
    return (
      <section className="panel editor-panel empty-editor">
        <div className="empty-illustration" aria-hidden>
          <span />
          <span />
          <span />
        </div>
        <h2>Başlamak için bir dosya seçin</h2>
        <p>Soldaki listeden bir <code>*.resx</code> veya <code>*.json</code> dosyası seçerek çevirileri düzenleyin.</p>
      </section>
    );
  }

  return (
    <section className="panel editor-panel">
      <div className="panel-head editor-head">
        <div>
          <h2>{selectedPath}</h2>
          <p>{entries.length} anahtar · {filtered.length} gösteriliyor</p>
        </div>
        <div className="editor-actions">
          <div className="search-field compact">
            <IconSearch className="search-icon" />
            <input
              type="search"
              placeholder="Anahtar veya değer ara..."
              value={filter}
              onChange={(e) => onFilterChange(e.target.value)}
            />
          </div>
          <button type="button" className="btn btn-secondary" disabled={busy} onClick={onSave}>
            <IconSave />
            Kaydet
          </button>
        </div>
      </div>

      <div className="entry-table">
        <div className="entry-row entry-header">
          <span>Anahtar</span>
                <span>Değer</span>
        </div>
        {filtered.map((entry) => (
          <div className="entry-row" key={entry.name}>
            <code title={entry.name}>{entry.name}</code>
            <textarea
              rows={2}
              value={entry.value}
              placeholder="Çeviri girin..."
              onChange={(e) => onEntryChange(entry.name, e.target.value)}
            />
          </div>
        ))}
        {filtered.length === 0 ? (
          <p className="entry-empty">Aramanızla eşleşen kayıt yok.</p>
        ) : null}
      </div>
    </section>
  );
}
