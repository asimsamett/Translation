import { IconGitPull } from "./Icons";

type HeaderProps = {
  userName: string;
  ssoEnabled: boolean;
  isSignedIn: boolean;
  busy: boolean;
  modeLabel: string;
  theme: "light" | "dark";
  onThemeChange: (theme: "light" | "dark") => void;
  onSignIn: () => void;
  onSignOut: () => void;
  onPull: () => void;
};

export function Header({
  userName,
  ssoEnabled,
  isSignedIn,
  busy,
  modeLabel,
  theme,
  onThemeChange,
  onSignIn,
  onSignOut,
  onPull
}: HeaderProps) {
  return (
    <header className="topbar">
      <div className="brand">
        <div className="brand-mark" aria-hidden>
          .RESX
        </div>
        <div>
          <h1>Translation Studio</h1>
          <p>Resource Manager kaynak düzenleyici </p>
        </div>
      </div>

      <div className="topbar-actions">
        <span className="chip">{modeLabel}</span>
        <div className="theme-switch" aria-label="Tema seçimi">
          <button
            type="button"
            className={theme === "light" ? "active" : ""}
            aria-pressed={theme === "light"}
            onClick={() => onThemeChange("light")}
          >
            Light
          </button>
          <button
            type="button"
            className={theme === "dark" ? "active" : ""}
            aria-pressed={theme === "dark"}
            onClick={() => onThemeChange("dark")}
          >
            Dark
          </button>
        </div>
        <span className="user-pill">{userName}</span>

        {ssoEnabled ? (
          isSignedIn ? (
            <button type="button" className="btn btn-ghost" onClick={onSignOut}>
              Çıkış
            </button>
          ) : (
            <button type="button" className="btn btn-ghost" onClick={onSignIn}>
              Kurumsal giriş
            </button>
          )
        ) : null}

        <button type="button" className="btn btn-primary" disabled={busy} onClick={onPull}>
          <IconGitPull />
          Repodan çek
        </button>
      </div>
    </header>
  );
}
