import type { CombatStatsLiveDto } from "../types";
import sample from "../sample-data.json";

export function loadEmbeddedLive(): CombatStatsLiveDto | null {
  if (typeof window !== "undefined" && window.__COMBAT_STATS__)
    return window.__COMBAT_STATS__;

  const el = document.getElementById("combat-stats-data");
  if (!el?.textContent)
    return import.meta.env.DEV ? (sample as CombatStatsLiveDto) : null;

  const raw = el.textContent.trim();
  if (!raw || !raw.startsWith("{"))
    return import.meta.env.DEV ? (sample as CombatStatsLiveDto) : null;

  try {
    return JSON.parse(raw) as CombatStatsLiveDto;
  }
  catch {
    return null;
  }
}

export function isLiveHost(): boolean {
  if (typeof window === "undefined")
    return false;
  if (import.meta.env.DEV)
    return false;
  const host = window.location.hostname;
  return host === "localhost" || host === "127.0.0.1";
}

export function wsUrl(): string {
  const proto = window.location.protocol === "https:" ? "wss:" : "ws:";
  return `${proto}//${window.location.host}/api/ws`;
}

export function logsWsUrl(): string {
  const proto = window.location.protocol === "https:" ? "wss:" : "ws:";
  return `${proto}//${window.location.host}/api/logs/ws`;
}

export function liveApiUrl(): string {
  return `${window.location.origin}/api/live`;
}

export function exportJsonUrl(): string {
  return `${window.location.origin}/api/export/json`;
}

function formatExportFilename(): string {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  return `combat-stats-${d.getFullYear()}${pad(d.getMonth() + 1)}${pad(d.getDate())}-${pad(d.getHours())}${pad(d.getMinutes())}${pad(d.getSeconds())}.json`;
}

function parseContentDispositionFilename(header: string | null): string | null {
  if (!header)
    return null;
  const match = header.match(/filename\*?=(?:UTF-8''|")?([^";]+)"?/i);
  return match?.[1] ? decodeURIComponent(match[1]) : null;
}

function triggerBrowserDownload(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = filename;
  anchor.rel = "noopener";
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

/** Download full combat stats bundle (live host) or embedded snapshot (offline). */
export async function downloadCombatStatsJson(fallbackLive?: CombatStatsLiveDto | null): Promise<string> {
  if (isLiveHost()) {
    const res = await fetch(exportJsonUrl(), { cache: "no-store" });
    if (!res.ok)
      throw new Error(`Export failed (${res.status})`);
    const filename = parseContentDispositionFilename(res.headers.get("Content-Disposition"))
      ?? formatExportFilename();
    const blob = await res.blob();
    triggerBrowserDownload(blob, filename);
    return filename;
  }

  if (!fallbackLive)
    throw new Error("No combat data to export");

  const filename = formatExportFilename();
  const json = `${JSON.stringify(fallbackLive, null, 2)}\n`;
  triggerBrowserDownload(new Blob([json], { type: "application/json;charset=utf-8" }), filename);
  return filename;
}

export async function fetchLiveSnapshot(): Promise<CombatStatsLiveDto | null> {
  try {
    const res = await fetch(liveApiUrl(), { cache: "no-store" });
    if (!res.ok)
      return null;
    const data = (await res.json()) as CombatStatsLiveDto;
    return data.active ? data : null;
  }
  catch {
    return null;
  }
}

export function formatAmount(kind: string, amount: number): string {
  if (amount <= 0)
    return "";
  if (kind === "BlockGained")
    return `+${amount}`;
  if (kind === "EnergySpent")
    return `-${amount}`;
  return String(amount);
}

export function splitEventText(text: string): { primary: string; secondary: string } {
  const arrow = " → ";
  const idx = text.indexOf(arrow);
  if (idx > 0)
    return { primary: text.slice(0, idx), secondary: text.slice(idx + arrow.length) };
  return { primary: text, secondary: "" };
}

export function localizeEventText(
  text: string,
  t: (key: string, values?: Record<string, unknown>) => string,
): string {
  const block = text.match(/^([+\-]?\d+)\s+block$/i);
  if (block)
    return t("events.blockText", { n: block[1] });

  const energy = text.match(/^([+\-]?\d+)\s+energy$/i);
  if (energy)
    return t("events.energyText", { n: energy[1] });

  return text;
}

export function eventSourceLabel(event: {
  sourceName?: string;
  sourceKind?: string;
  text: string;
  kind: string;
}): string {
  if (!event.sourceName?.trim())
    return "";

  const source = event.sourceName.trim();
  if (event.kind === "CardPlayed" && event.text.trim() === source)
    return "";

  const parts = splitEventText(event.text);
  if (parts.primary === source || parts.primary.startsWith(`${source} `))
    return "";

  return source;
}

export function findPlayerSummary(
  snapshot: CombatStatsLiveDto["active"],
  playerKey: string | null,
) {
  if (!snapshot?.players.length)
    return null;
  if (!playerKey)
    return snapshot.players[0] ?? null;
  return snapshot.players.find((p) => p.key === playerKey) ?? snapshot.players[0] ?? null;
}
