import { IconAlert, IconCheck } from "./Icons";

export type ToastKind = "success" | "error";

export type ToastMessage = {
  id: number;
  kind: ToastKind;
  text: string;
};

export function ToastStack({ toasts, onDismiss }: { toasts: ToastMessage[]; onDismiss: (id: number) => void }) {
  if (toasts.length === 0) return null;

  return (
    <div className="toast-stack" role="alert" aria-live="assertive">
      {toasts.map((t) => (
        <div key={t.id} className={`toast toast-${t.kind}`}>
          <span className="toast-icon">{t.kind === "success" ? <IconCheck /> : <IconAlert />}</span>
          <p>{t.text}</p>
          <button type="button" className="btn btn-ghost btn-sm toast-accept" onClick={() => onDismiss(t.id)}>
            Accept
          </button>
        </div>
      ))}
    </div>
  );
}
