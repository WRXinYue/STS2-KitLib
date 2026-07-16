const LOG_LEVEL_BRACKET_TAGS = new Set([
  "info", "warn", "warning", "error", "debug", "load", "verydebug", "vdb", "dbg",
]);

export function looksLikeModOrScopeTag(inner: string): boolean {
  if (!inner.trim())
    return false;
  return !inner.includes("=") && !inner.includes(",");
}

function normalizeModIdKey(id: string): string {
  return id.toLowerCase().replaceAll("-", "_");
}

function tryResolveModIdKey(
  normalizedKey: string,
  modIdAliases: Record<string, string>,
  out: { modId: string },
): boolean {
  const resolved = modIdAliases[normalizedKey];
  if (resolved) {
    out.modId = resolved;
    return true;
  }
  return false;
}

function tryResolveModId(
  candidate: string,
  loadedModIds: Set<string>,
  modIdAliases: Record<string, string>,
): string | null {
  if (LOG_LEVEL_BRACKET_TAGS.has(candidate.toLowerCase()))
    return null;
  if (!looksLikeModOrScopeTag(candidate))
    return null;
  if (loadedModIds.has(candidate))
    return candidate;

  const out = { modId: "" };
  if (tryResolveModIdKey(normalizeModIdKey(candidate), modIdAliases, out))
    return out.modId;

  const lastDot = candidate.lastIndexOf(".");
  if (lastDot >= 0 && lastDot < candidate.length - 1) {
    const suffix = candidate.slice(lastDot + 1);
    if (tryResolveModIdKey(normalizeModIdKey(suffix), modIdAliases, out))
      return out.modId;
  }

  return null;
}

export function tryFindModTagSpan(
  text: string,
  loadedModIds: Set<string>,
  modIdAliases: Record<string, string>,
): { tagStart: number; tagEnd: number; modId: string } | null {
  let i = 0;
  while (i < text.length) {
    const open = text.indexOf("[", i);
    if (open < 0)
      break;
    const close = text.indexOf("]", open + 1);
    if (close <= open + 1) {
      i = open + 1;
      continue;
    }
    const inner = text.slice(open + 1, close);
    const modId = tryResolveModId(inner, loadedModIds, modIdAliases);
    if (modId && looksLikeModOrScopeTag(inner))
      return { tagStart: open, tagEnd: close + 1, modId };
    i = close + 1;
  }
  return null;
}

export function tryFindAnyModTagSpan(
  text: string,
): { tagStart: number; tagEnd: number; tagInner: string } | null {
  let i = 0;
  while (i < text.length) {
    const open = text.indexOf("[", i);
    if (open < 0)
      break;
    const close = text.indexOf("]", open + 1);
    if (close <= open + 1) {
      i = open + 1;
      continue;
    }
    const inner = text.slice(open + 1, close);
    if (looksLikeModOrScopeTag(inner) && !LOG_LEVEL_BRACKET_TAGS.has(inner.toLowerCase()))
      return { tagStart: open, tagEnd: close + 1, tagInner: inner };
    i = close + 1;
  }
  return null;
}

export function tryFindSecondaryTagSpan(
  text: string,
  searchFrom: number,
  primaryModId: string,
  loadedModIds: Set<string> | null,
  modIdAliases: Record<string, string> | null,
): { tagStart: number; tagEnd: number; isContentModTag: boolean; tagInner: string } | null {
  let pos = searchFrom;
  while (pos < text.length) {
    while (pos < text.length && /\s/.test(text[pos] ?? ""))
      pos++;
    if (pos >= text.length || text[pos] !== "[")
      return null;

    const open = pos;
    const close = text.indexOf("]", open + 1);
    if (close <= open + 1) {
      pos = open + 1;
      continue;
    }

    const tagInner = text.slice(open + 1, close);
    if (LOG_LEVEL_BRACKET_TAGS.has(tagInner.toLowerCase())) {
      pos = close + 1;
      continue;
    }
    if (tagInner.toLowerCase() === primaryModId.toLowerCase()) {
      pos = close + 1;
      continue;
    }
    if (!looksLikeModOrScopeTag(tagInner)) {
      pos = close + 1;
      continue;
    }

    let isContentModTag = false;
    if (loadedModIds && loadedModIds.size > 0 && modIdAliases) {
      const resolved = tryResolveModId(tagInner, loadedModIds, modIdAliases);
      isContentModTag = resolved != null;
    }

    return { tagStart: open, tagEnd: close + 1, isContentModTag, tagInner };
  }
  return null;
}
