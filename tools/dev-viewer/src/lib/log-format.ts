import type { LogEntryDto, LogViewerFilterDto } from "../types";
import {
  ansiScopeDim,
  ansiTrueColorFg,
  appendAnsiSegment,
  COL_BOUNDARY,
  COL_TIME,
  dimHex,
  levelColorHex,
} from "./log-ansi";
import { modColorHex } from "./log-mod-colors";
import {
  tryFindAnyModTagSpan,
  tryFindModTagSpan,
  tryFindSecondaryTagSpan,
} from "./log-tag-matcher";
import { shouldShowLogEntry } from "./log-filter-state";

const DEFAULT_HOST_MOD_ID = "KitLib";
const GAME_DIM_AMOUNT = 0.18;

function formatLocalTime(ts: number | string): string {
  if (typeof ts === "number")
    return new Date(ts).toLocaleTimeString(undefined, { hour12: false });
  return ts.length > 8 ? ts.slice(11, 19) : ts;
}

function levelBadge(lvl: string): string {
  switch (lvl.toLowerCase()) {
    case "error":
      return "ERR ";
    case "warn":
    case "warning":
      return "WARN";
    case "load":
      return "LOAD";
    case "debug":
    case "dbg":
      return "DBG ";
    case "vdb":
    case "verydebug":
      return "VDB ";
    default:
      return "INFO";
  }
}

function skipWhitespace(text: string, pos: number): number {
  while (pos < text.length && /\s/.test(text[pos] ?? ""))
    pos++;
  return pos;
}

function viewerState(filter: LogViewerFilterDto | null) {
  return {
    loadedModIds: new Set(filter?.loadedModIds ?? []),
    modIdAliases: filter?.modIdAliases ?? {},
  };
}

function isGameSource(entry: LogEntryDto): boolean {
  return !entry.mod?.trim();
}

function resolveLevelAnsi(entry: LogEntryDto): string {
  const hex = levelColorHex(entry.lvl);
  const effective = isGameSource(entry) ? dimHex(hex, GAME_DIM_AMOUNT) : hex;
  return ansiTrueColorFg(effective);
}

function resolveTimeAnsi(entry: LogEntryDto): string {
  const hex = isGameSource(entry) ? dimHex(COL_TIME, GAME_DIM_AMOUNT) : COL_TIME;
  return ansiTrueColorFg(hex);
}

function buildHeader(entry: LogEntryDto): string[] {
  const parts: string[] = [];
  appendAnsiSegment(parts, resolveTimeAnsi(entry), `${formatLocalTime(entry.ts)} `);
  appendAnsiSegment(parts, resolveLevelAnsi(entry), levelBadge(entry.lvl));
  parts.push(" ");
  return parts;
}

function buildTextBodyAnsi(
  text: string,
  levelAnsi: string,
  loadedModIds: Set<string>,
  modIdAliases: Record<string, string>,
): string[] {
  const parts: string[] = [];

  const modTag = loadedModIds.size > 0
    ? tryFindModTagSpan(text, loadedModIds, modIdAliases)
    : null;
  const anyTag = modTag ?? (() => {
    const found = tryFindAnyModTagSpan(text);
    return found ? { ...found, modId: found.tagInner } : null;
  })();

  if (!anyTag) {
    appendAnsiSegment(parts, levelAnsi, text);
    return parts;
  }

  const { tagStart, tagEnd, modId } = anyTag;
  appendAnsiSegment(parts, levelAnsi, text.slice(0, tagStart));
  appendAnsiSegment(parts, ansiTrueColorFg(modColorHex(modId)), text.slice(tagStart, tagEnd));

  let pos = tagEnd;
  while (true) {
    const secondary = tryFindSecondaryTagSpan(
      text,
      pos,
      modId,
      loadedModIds.size > 0 ? loadedModIds : null,
      modIdAliases,
    );
    if (!secondary)
      break;

    if (secondary.tagStart > pos)
      appendAnsiSegment(parts, levelAnsi, text.slice(pos, secondary.tagStart));
    else if (secondary.tagStart === pos && text[pos] === "[")
      parts.push(" ");

    const secondaryAnsi = secondary.isContentModTag
      ? ansiTrueColorFg(modColorHex(secondary.tagInner))
      : ansiScopeDim();
    appendAnsiSegment(parts, secondaryAnsi, text.slice(secondary.tagStart, secondary.tagEnd));
    pos = secondary.tagEnd;
  }

  appendAnsiSegment(parts, levelAnsi, text.slice(pos));
  return parts;
}

function buildStructuredBodyAnsi(entry: LogEntryDto, levelAnsi: string): string[] {
  const parts: string[] = [];
  const mod = entry.mod!.trim();
  const modAnsi = ansiTrueColorFg(modColorHex(mod));
  const text = entry.text;
  let pos = 0;

  const hostTag = `[${DEFAULT_HOST_MOD_ID}]`;
  if (text.toLowerCase().startsWith(hostTag.toLowerCase())) {
    appendAnsiSegment(parts, modAnsi, hostTag);
    pos = hostTag.length;
    pos = skipWhitespace(text, pos);
  }

  const modTag = `[${mod}]`;
  if (mod.toLowerCase() !== DEFAULT_HOST_MOD_ID.toLowerCase()
    && text.slice(pos).startsWith(modTag)) {
    appendAnsiSegment(parts, modAnsi, modTag);
    pos += modTag.length;
  }

  if (entry.scope?.trim()) {
    const gapStart = pos;
    pos = skipWhitespace(text, pos);
    appendAnsiSegment(parts, levelAnsi, text.slice(gapStart, pos));

    const scopeTag = `[${entry.scope.trim()}]`;
    if (text.slice(pos).startsWith(scopeTag)) {
      appendAnsiSegment(parts, ansiScopeDim(), scopeTag);
      pos += scopeTag.length;
    }
  }

  appendAnsiSegment(parts, levelAnsi, text.slice(pos));
  return parts;
}

function buildBoundaryLine(entry: LogEntryDto): string {
  const parts: string[] = [];
  const accent = ansiTrueColorFg(COL_BOUNDARY);
  appendAnsiSegment(parts, accent, `${formatLocalTime(entry.ts)} ${levelBadge(entry.lvl)} `);
  appendAnsiSegment(parts, accent, entry.text);
  return parts.join("");
}

export function formatLogLine(entry: LogEntryDto, filter: LogViewerFilterDto | null = null): string {
  if (entry.boundary)
    return buildBoundaryLine(entry);

  const levelAnsi = resolveLevelAnsi(entry);
  const { loadedModIds, modIdAliases } = viewerState(filter);
  const parts = buildHeader(entry);

  if (entry.mod?.trim())
    parts.push(...buildStructuredBodyAnsi(entry, levelAnsi));
  else
    parts.push(...buildTextBodyAnsi(entry.text, levelAnsi, loadedModIds, modIdAliases));

  return parts.join("");
}

export function matchesAiPreset(text: string): boolean {
  return /\[(AutoPlay|AiHost|MpAi|LanLocal|Companion)/.test(text);
}

export function passesLogFilter(
  entry: LogEntryDto,
  preset: string | null,
  serverFilter: LogViewerFilterDto | null,
): boolean {
  return shouldShowLogEntry(entry, serverFilter, { aiPreset: preset === "ai" });
}
