import type { Terminal } from "@xterm/xterm";

const ANSI_RE = /\x1b\[[0-9;?]*[ -/]*[@-~]/g;

export function stripAnsi(text: string): string {
  return text.replace(ANSI_RE, "");
}

/** Wire browser copy/paste for an xterm log view (Ctrl/Cmd+C with selection, native copy event). */
export function attachTerminalClipboard(term: Terminal, host: HTMLElement): () => void {
  const onCopy = (ev: ClipboardEvent) => {
    const sel = term.getSelection();
    if (!sel)
      return;
    ev.clipboardData?.setData("text/plain", stripAnsi(sel));
    ev.preventDefault();
  };

  const customKey = (ev: KeyboardEvent) => {
    if (ev.type !== "keydown")
      return true;

    const mod = ev.ctrlKey || ev.metaKey;
    if (!mod)
      return true;

    if (ev.key === "c" || ev.key === "C") {
      const sel = term.getSelection();
      if (sel) {
        void navigator.clipboard.writeText(stripAnsi(sel));
        return false;
      }
    }

    if ((ev.key === "v" || ev.key === "V") && !ev.shiftKey)
      return false;

    return true;
  };

  term.attachCustomKeyEventHandler(customKey);
  host.addEventListener("copy", onCopy);

  return () => host.removeEventListener("copy", onCopy);
}
