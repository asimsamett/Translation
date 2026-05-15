import { useCallback, useRef, useState } from "react";
import type { ToastKind, ToastMessage } from "../components/Toast";

export function useToast() {
  const [toasts, setToasts] = useState<ToastMessage[]>([]);
  const idRef = useRef(0);

  const dismiss = useCallback((id: number) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const push = useCallback((kind: ToastKind, text: string) => {
    const id = ++idRef.current;
    setToasts((prev) =>
      prev.some((toast) => toast.kind === kind && toast.text === text) ? prev : [...prev, { id, kind, text }]
    );
  }, []);

  return { toasts, dismiss, push };
}
