import { onMounted, onUnmounted, ref, type Ref } from "vue";
import type { CombatStatsLiveDto } from "@/types";
import { downloadCombatStatsJson, fetchLiveSnapshot, isLiveHost, loadEmbeddedLive, wsUrl } from "@/lib/format";

const RECONNECT_MS = 3000;

export function useLiveCombatStats(): {
  live: Ref<CombatStatsLiveDto | null>;
  connected: Ref<boolean>;
  exportStatus: Ref<string>;
  downloadJson: () => Promise<void>;
  isLiveHost: boolean;
} {
  const isLive = isLiveHost();
  const live = ref<CombatStatsLiveDto | null>(isLive ? null : loadEmbeddedLive());
  const connected = ref(false);
  const exportStatus = ref("");
  let socket: WebSocket | null = null;
  let reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  let disposed = false;
  let lastRevision = 0;

  function applyStats(payload: CombatStatsLiveDto, revision?: number) {
    if (revision != null && revision < lastRevision && live.value != null)
      return;
    if (revision != null)
      lastRevision = revision;
    live.value = payload;
  }

  function send(msg: { type: "ping" } | { type: "requestStats" }) {
    if (!socket || socket.readyState !== WebSocket.OPEN)
      return;
    socket.send(JSON.stringify(msg));
  }

  async function downloadJson() {
    exportStatus.value = "";
    const filename = await downloadCombatStatsJson(live.value);
    exportStatus.value = filename;
  }

  async function hydrateFromDisk() {
    const cached = await fetchLiveSnapshot();
    if (cached)
      applyStats(cached);
  }

  function connect() {
    if (!isLive || disposed)
      return;

    socket = new WebSocket(wsUrl());

    socket.onopen = () => {
      connected.value = true;
      send({ type: "requestStats" });
    };

    socket.onmessage = (ev) => {
      try {
        const msg = JSON.parse(String(ev.data)) as {
          type: string;
          payload?: CombatStatsLiveDto;
          revision?: number;
        };
        if (msg.type === "stats")
          applyStats(msg.payload!, msg.revision);
        else if (msg.type === "hello")
          send({ type: "requestStats" });
      }
      catch {
        // ignore malformed frames
      }
    };

    socket.onclose = () => {
      connected.value = false;
      socket = null;
      if (!disposed) {
        reconnectTimer = setTimeout(connect, RECONNECT_MS);
      }
    };

    socket.onerror = () => {
      socket?.close();
    };
  }

  onMounted(async () => {
    if (!isLive)
      return;
    await hydrateFromDisk();
    connect();
  });

  onUnmounted(() => {
    disposed = true;
    if (reconnectTimer)
      clearTimeout(reconnectTimer);
    socket?.close();
    socket = null;
  });

  return { live, connected, exportStatus, downloadJson, isLiveHost: isLive };
}
