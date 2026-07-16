import { computed, ref, shallowRef } from "vue";
import type { LogEntryDto, LogViewerFilterDto } from "../types";
import { logsWsUrl } from "../lib/format";
import {
  cloneFilter,
  computeLogStats,
  defaultLogViewerFilter,
  filtersEqual,
} from "../lib/log-filter-state";

export type LogConnectionState = "connecting" | "connected" | "disconnected";

export function useLogViewer() {
  const state = ref<LogConnectionState>("connecting");
  const preset = ref<string | null>(null);
  const serverFilter = ref<LogViewerFilterDto | null>(null);
  const localFilter = ref<LogViewerFilterDto>(defaultLogViewerFilter());
  const syncWithGame = ref(true);
  const entries = shallowRef<LogEntryDto[]>([]);

  const effectiveFilter = computed<LogViewerFilterDto>(() => {
    if (syncWithGame.value && serverFilter.value)
      return cloneFilter(serverFilter.value);
    return cloneFilter(localFilter.value);
  });

  const stats = computed(() =>
    computeLogStats(entries.value, effectiveFilter.value, preset.value === "ai"),
  );

  let ws: WebSocket | null = null;
  let reconnectTimer: ReturnType<typeof setTimeout> | null = null;

  function readPresetFromUrl(): string | null {
    const params = new URLSearchParams(location.search);
    const fromSearch = params.get("preset");
    if (fromSearch)
      return fromSearch;
    const hash = location.hash.replace(/^#\/?/, "");
    const q = hash.indexOf("?");
    if (q >= 0)
      return new URLSearchParams(hash.slice(q + 1)).get("preset");
    return null;
  }

  function pushEntry(entry: LogEntryDto) {
    entries.value = [...entries.value, entry];
  }

  function handleMessage(raw: string) {
    let msg: { type: string; entry?: LogEntryDto; filter?: LogViewerFilterDto | null };
    try {
      msg = JSON.parse(raw);
    }
    catch {
      return;
    }
    if (msg.type === "hello") {
      state.value = "connected";
      return;
    }
    if (msg.type === "filter") {
      const next = msg.filter ?? null;
      if (filtersEqual(serverFilter.value, next))
        return;
      serverFilter.value = next;
      if (syncWithGame.value && serverFilter.value)
        localFilter.value = cloneFilter(serverFilter.value);
      return;
    }
    if (msg.type === "log" && msg.entry)
      pushEntry(msg.entry);
  }

  function connect() {
    if (ws) {
      ws.close();
      ws = null;
    }
    state.value = "connecting";
    ws = new WebSocket(logsWsUrl());
    ws.onopen = () => {
      state.value = "connected";
    };
    ws.onmessage = (ev) => handleMessage(String(ev.data));
    ws.onclose = () => {
      state.value = "disconnected";
      scheduleReconnect();
    };
    ws.onerror = () => ws?.close();
  }

  function scheduleReconnect() {
    if (reconnectTimer)
      return;
    reconnectTimer = setTimeout(() => {
      reconnectTimer = null;
      connect();
    }, 2000);
  }

  function setSyncWithGame(on: boolean) {
    syncWithGame.value = on;
    if (on && serverFilter.value)
      localFilter.value = cloneFilter(serverFilter.value);
  }

  function updateLocalFilter(patch: Partial<LogViewerFilterDto>) {
    syncWithGame.value = false;
    localFilter.value = {
      ...cloneFilter(localFilter.value),
      ...patch,
      hiddenSources: patch.hiddenSources ?? localFilter.value.hiddenSources,
      suppressRules: patch.suppressRules ?? localFilter.value.suppressRules,
      loadedModIds: patch.loadedModIds ?? localFilter.value.loadedModIds,
      modIdAliases: patch.modIdAliases ?? localFilter.value.modIdAliases,
    };
  }

  preset.value = readPresetFromUrl();
  connect();

  return {
    state,
    preset,
    entries,
    serverFilter,
    localFilter,
    syncWithGame,
    effectiveFilter,
    stats,
    setSyncWithGame,
    updateLocalFilter,
    disconnect: () => {
      if (reconnectTimer)
        clearTimeout(reconnectTimer);
      ws?.close();
      ws = null;
    },
  };
}
