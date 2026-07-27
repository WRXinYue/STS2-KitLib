import { onMounted, onUnmounted, ref, type Ref } from "vue";
import type { AiDecisionLiveDto } from "@/types";
import { aiWsUrl, fetchAiLiveSnapshot, isLiveHost } from "@/lib/format";

const RECONNECT_MS = 3000;

export function useLiveAiDecision(): {
  live: Ref<AiDecisionLiveDto | null>;
  connected: Ref<boolean>;
  isLiveHost: boolean;
} {
  const isLive = isLiveHost();
  const live = ref<AiDecisionLiveDto | null>(null);
  const connected = ref(false);
  let socket: WebSocket | null = null;
  let reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  let disposed = false;
  let lastRevision = 0;

  function applyAi(payload: AiDecisionLiveDto, revision?: number) {
    if (revision != null && revision < lastRevision && live.value != null)
      return;
    if (revision != null)
      lastRevision = revision;
    live.value = payload;
  }

  function send(msg: { type: "ping" } | { type: "requestAi" }) {
    if (!socket || socket.readyState !== WebSocket.OPEN)
      return;
    socket.send(JSON.stringify(msg));
  }

  async function hydrateFromDisk() {
    const cached = await fetchAiLiveSnapshot();
    if (cached)
      applyAi(cached);
  }

  function connect() {
    if (!isLive || disposed)
      return;

    socket = new WebSocket(aiWsUrl());

    socket.onopen = () => {
      connected.value = true;
      send({ type: "requestAi" });
    };

    socket.onmessage = (ev) => {
      try {
        const msg = JSON.parse(String(ev.data)) as {
          type: string;
          payload?: AiDecisionLiveDto;
          revision?: number;
        };
        if (msg.type === "ai" && msg.payload)
          applyAi(msg.payload, msg.revision);
        else if (msg.type === "hello")
          send({ type: "requestAi" });
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

  return { live, connected, isLiveHost: isLive };
}
