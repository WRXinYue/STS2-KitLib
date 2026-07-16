import type { LogEntryDto, LogViewerFilterDto, SuppressRuleDto } from "../types";
import { tryFindModTagSpan } from "./log-tag-matcher";

/** Host framework id for structured KitLog lines — not in ModRuntime.Catalog. */
export const HOST_MOD_SOURCE = "KitLib";

/** Built-in suppress patterns — keep in sync with LogSuppressor.cs */
export const BUILTIN_SUPPRESS_RULES: SuppressRuleDto[] = [
  { pattern: "AtlasResourceLoader: Missing sprite", enabled: true },
  { pattern: "Asset not cached:", enabled: true },
  { pattern: "[Assets] Missing resource path", enabled: true },
  { pattern: "Found mod manifest file", enabled: true },
  { pattern: "missing the 'id' field", enabled: true },
  { pattern: "warmup job failed", enabled: true },
  { pattern: "Limiting background FPS", enabled: true },
  { pattern: "Restored foreground FPS", enabled: true },
  { pattern: "The InputMap action", enabled: true },
];

export function defaultLogViewerFilter(): LogViewerFilterDto {
  return {
    minLevel: null,
    textFilter: "",
    hiddenSources: [],
    loadedModIds: [],
    modIdAliases: {},
    suppressRules: BUILTIN_SUPPRESS_RULES.map((r) => ({ ...r })),
  };
}

export function cloneFilter(filter: LogViewerFilterDto | null): LogViewerFilterDto {
  if (!filter)
    return defaultLogViewerFilter();
  return {
    minLevel: filter.minLevel ?? null,
    textFilter: filter.textFilter ?? "",
    hiddenSources: [...(filter.hiddenSources ?? [])],
    loadedModIds: [...(filter.loadedModIds ?? [])],
    modIdAliases: { ...(filter.modIdAliases ?? {}) },
    suppressRules: (filter.suppressRules ?? BUILTIN_SUPPRESS_RULES).map((r) => ({ ...r })),
  };
}

export function filtersEqual(a: LogViewerFilterDto | null, b: LogViewerFilterDto | null): boolean {
  if (a === b)
    return true;
  if (!a || !b)
    return false;
  if ((a.minLevel ?? null) !== (b.minLevel ?? null))
    return false;
  if ((a.textFilter ?? "") !== (b.textFilter ?? ""))
    return false;

  const hiddenA = [...(a.hiddenSources ?? [])].sort();
  const hiddenB = [...(b.hiddenSources ?? [])].sort();
  if (hiddenA.length !== hiddenB.length || hiddenA.some((v, i) => v !== hiddenB[i]))
    return false;

  const modsA = [...(a.loadedModIds ?? [])].sort();
  const modsB = [...(b.loadedModIds ?? [])].sort();
  if (modsA.length !== modsB.length || modsA.some((v, i) => v !== modsB[i]))
    return false;

  const aliasesA = a.modIdAliases ?? {};
  const aliasesB = b.modIdAliases ?? {};
  const aliasKeysA = Object.keys(aliasesA).sort();
  const aliasKeysB = Object.keys(aliasesB).sort();
  if (aliasKeysA.length !== aliasKeysB.length || aliasKeysA.some((k, i) => k !== aliasKeysB[i]))
    return false;
  if (aliasKeysA.some((k) => aliasesA[k] !== aliasesB[k]))
    return false;

  const rulesA = a.suppressRules ?? BUILTIN_SUPPRESS_RULES;
  const rulesB = b.suppressRules ?? BUILTIN_SUPPRESS_RULES;
  if (rulesA.length !== rulesB.length)
    return false;
  for (let i = 0; i < rulesA.length; i++) {
    if (rulesA[i]!.pattern !== rulesB[i]!.pattern || rulesA[i]!.enabled !== rulesB[i]!.enabled)
      return false;
  }

  return true;
}

function levelSeverity(lvl: string): number {
  switch (lvl.toLowerCase()) {
    case "error":
      return 4;
    case "warn":
    case "warning":
      return 3;
    case "info":
    case "load":
      return 2;
    case "debug":
    case "dbg":
      return 1;
    case "vdb":
    case "verydebug":
      return 0;
    default:
      return 1;
  }
}

export function meetsMinLevel(lvl: string, minLevel: string | null | undefined): boolean {
  if (!minLevel)
    return true;
  const minSev = ({ info: 2, warn: 3, warning: 3, error: 4 } as Record<string, number>)[minLevel.toLowerCase()] ?? 0;
  return levelSeverity(lvl) >= minSev;
}

export function isSessionBoundary(entry: LogEntryDto): boolean {
  if (entry.boundary)
    return true;
  return /DevMode log capture|SessionBoundary|\[pid=/.test(entry.text);
}

export function parseLogSource(entry: LogEntryDto, filter: LogViewerFilterDto | null): string {
  if (entry.mod?.trim())
    return entry.mod.trim();

  const loaded = new Set(filter?.loadedModIds ?? []);
  const aliases = filter?.modIdAliases ?? {};
  if (loaded.size === 0)
    return "Game";

  const tag = tryFindModTagSpan(entry.text, loaded, aliases);
  return tag?.modId ?? "Game";
}

export function isSuppressedByRules(
  text: string,
  rules: SuppressRuleDto[] | null | undefined,
  hitCounts?: Record<string, number>,
): boolean {
  for (const rule of rules ?? []) {
    if (!rule.enabled || !rule.pattern)
      continue;
    if (text.toLowerCase().includes(rule.pattern.toLowerCase())) {
      if (hitCounts)
        hitCounts[rule.pattern] = (hitCounts[rule.pattern] ?? 0) + 1;
      return true;
    }
  }
  return false;
}

export function shouldShowLogEntry(
  entry: LogEntryDto,
  filter: LogViewerFilterDto | null,
  options?: { aiPreset?: boolean; suppressHits?: Record<string, number> },
): boolean {
  if (options?.aiPreset && !/\[(AutoPlay|AiHost|MpAi|LanLocal|Companion)/.test(entry.text))
    return false;

  if (isSessionBoundary(entry))
    return true;

  if (!meetsMinLevel(entry.lvl, filter?.minLevel))
    return false;

  const textFilter = filter?.textFilter?.trim();
  if (textFilter && !entry.text.toLowerCase().includes(textFilter.toLowerCase()))
    return false;

  if (isSuppressedByRules(entry.text, filter?.suppressRules, options?.suppressHits))
    return false;

  const source = parseLogSource(entry, filter);
  if (filter?.hiddenSources?.includes(source))
    return false;

  return true;
}

export interface LogViewerStats {
  visibleCount: number;
  suppressedCount: number;
  bySource: Record<string, number>;
  byLevel: Record<string, number>;
  ruleHits: Record<string, number>;
}

export function computeLogStats(
  entries: LogEntryDto[],
  filter: LogViewerFilterDto | null,
  aiPreset: boolean,
): LogViewerStats {
  const bySource: Record<string, number> = {};
  const byLevel: Record<string, number> = {};
  const ruleHits: Record<string, number> = {};
  let visibleCount = 0;
  let suppressedCount = 0;

  for (const entry of entries) {
    if (isSessionBoundary(entry)) {
      visibleCount++;
      continue;
    }

    const hits: Record<string, number> = {};
    const suppressed = isSuppressedByRules(entry.text, filter?.suppressRules, hits);
    for (const [k, v] of Object.entries(hits))
      ruleHits[k] = (ruleHits[k] ?? 0) + v;

    if (!shouldShowLogEntry(entry, filter, { aiPreset })) {
      if (suppressed)
        suppressedCount++;
      continue;
    }

    visibleCount++;
    const source = parseLogSource(entry, filter);
    bySource[source] = (bySource[source] ?? 0) + 1;
    const lvl = entry.lvl.toUpperCase();
    byLevel[lvl] = (byLevel[lvl] ?? 0) + 1;
  }

  return { visibleCount, suppressedCount, bySource, byLevel, ruleHits };
}

export function listModSources(
  filter: LogViewerFilterDto | null,
  discoveredSources?: Iterable<string>,
): string[] {
  const mods = filter?.loadedModIds ?? [];
  const sources = new Set<string>(["Game", HOST_MOD_SOURCE, ...mods]);
  for (const hidden of filter?.hiddenSources ?? [])
    sources.add(hidden);
  for (const source of discoveredSources ?? [])
    sources.add(source);
  return [...sources].sort((a, b) => {
    if (a === "Game")
      return -1;
    if (b === "Game")
      return 1;
    if (a === HOST_MOD_SOURCE)
      return -1;
    if (b === HOST_MOD_SOURCE)
      return 1;
    return a.localeCompare(b);
  });
}

export function isSourceVisible(source: string, filter: LogViewerFilterDto | null): boolean {
  return !(filter?.hiddenSources ?? []).includes(source);
}

export function shortRuleLabel(pattern: string): string {
  const cut = pattern.indexOf(":");
  if (cut > 0 && cut <= 28)
    return pattern.slice(0, cut);
  return pattern.length > 24 ? `${pattern.slice(0, 22)}…` : pattern;
}
