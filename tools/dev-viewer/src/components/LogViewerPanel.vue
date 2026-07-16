<script setup lang="ts">
import { nextTick, onMounted, onUnmounted, watch } from "vue";
import { Terminal } from "@xterm/xterm";
import { FitAddon } from "@xterm/addon-fit";
import "@xterm/xterm/css/xterm.css";
import { useI18n } from "vue-i18n";
import { useLogViewer } from "../composables/useLogViewer";
import { formatLogLine } from "../lib/log-format";
import { attachTerminalClipboard, stripAnsi } from "../lib/log-clipboard";
import { shouldShowLogEntry } from "../lib/log-filter-state";
import LogFilterSidebar from "./LogFilterSidebar.vue";

const props = defineProps<{
  active?: boolean;
}>();

const { t } = useI18n();

const {
  state,
  preset,
  entries,
  syncWithGame,
  effectiveFilter,
  stats,
  setSyncWithGame,
  updateLocalFilter,
  disconnect,
} = useLogViewer();

let host: HTMLElement | null = null;
let term: Terminal | null = null;
let fit: FitAddon | null = null;
let detachClipboard: (() => void) | null = null;

function setHost(el: HTMLElement | null) {
  host = el;
}

function rerenderTerminal() {
  if (!term)
    return;
  term.reset();
  const filter = effectiveFilter.value;
  const aiPreset = preset.value === "ai";
  for (const entry of entries.value) {
    if (shouldShowLogEntry(entry, filter, { aiPreset }))
      term.writeln(formatLogLine(entry, filter));
  }
}

function appendIfVisible(entry: typeof entries.value[number]) {
  if (!term)
    return;
  const filter = effectiveFilter.value;
  if (shouldShowLogEntry(entry, filter, { aiPreset: preset.value === "ai" }))
    term.writeln(formatLogLine(entry, filter));
}

function fitTerminal() {
  fit?.fit();
}

function copyVisibleLogs() {
  if (!term)
    return;
  const filter = effectiveFilter.value;
  const aiPreset = preset.value === "ai";
  const lines: string[] = [];
  for (const entry of entries.value) {
    if (shouldShowLogEntry(entry, filter, { aiPreset }))
      lines.push(stripAnsi(formatLogLine(entry, filter)));
  }
  if (lines.length === 0)
    return;
  void navigator.clipboard.writeText(lines.join("\n"));
}

watch(entries, (list, prev) => {
  if (!term || list.length <= (prev?.length ?? 0))
    return;
  for (let i = prev?.length ?? 0; i < list.length; i++)
    appendIfVisible(list[i]!);
}, { flush: "post" });

watch(effectiveFilter, () => rerenderTerminal(), { deep: true });

watch(() => props.active, (visible) => {
  if (visible)
    nextTick(() => fitTerminal());
});

onMounted(() => {
  if (!host)
    return;
  term = new Terminal({
    theme: {
      background: "#0d1117",
      foreground: "#c9d1d9",
      cursor: "#58a6ff",
      selectionBackground: "#264f78",
    },
    fontFamily: '"Cascadia Code", "Consolas", "Monaco", monospace',
    fontSize: 13,
    lineHeight: 1.25,
    scrollback: 10000,
    convertEol: true,
  });
  fit = new FitAddon();
  term.loadAddon(fit);
  term.open(host);
  detachClipboard = attachTerminalClipboard(term, host);
  rerenderTerminal();
  fitTerminal();
  window.addEventListener("resize", fitTerminal);
});

onUnmounted(() => {
  window.removeEventListener("resize", fitTerminal);
  detachClipboard?.();
  detachClipboard = null;
  disconnect();
  term?.dispose();
  term = null;
  fit = null;
});
</script>

<template>
  <div class="log-viewer">
    <LogFilterSidebar
      :filter="effectiveFilter"
      :sync-with-game="syncWithGame"
      :stats="stats"
      @update:sync-with-game="setSyncWithGame"
      @update-filter="updateLocalFilter"
    />

    <div class="log-main">
      <header class="log-toolbar">
        <span
          class="log-status"
          :data-state="state"
        >{{ state }}</span>
        <span
          v-if="preset"
          class="log-preset"
        >preset: {{ preset }}</span>
        <span
          v-if="syncWithGame"
          class="log-sync-badge"
        >{{ t("logs.syncActive") }}</span>
        <button
          type="button"
          class="log-copy-btn"
          :title="t('logs.copyAll')"
          @click="copyVisibleLogs"
        >
          {{ t("logs.copyAll") }}
        </button>
      </header>
      <div
        :ref="setHost"
        class="log-terminal"
      />
    </div>
  </div>
</template>

<style scoped>
.log-viewer {
  display: flex;
  flex: 1;
  min-height: 0;
  width: 100%;
  background: #0d1117;
}

.log-main {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-width: 0;
  min-height: 0;
}

.log-toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
  padding: 6px 12px;
  font-size: 12px;
  color: #8b949e;
  border-bottom: 1px solid #21262d;
  flex-shrink: 0;
}

.log-status[data-state="connected"] {
  color: #3fb950;
}

.log-status[data-state="connecting"] {
  color: #d29922;
}

.log-status[data-state="disconnected"] {
  color: #f85149;
}

.log-preset,
.log-sync-badge {
  color: #58a6ff;
}

.log-copy-btn {
  margin-left: auto;
  padding: 4px 10px;
  border-radius: 6px;
  border: 1px solid #30363d;
  background: #21262d;
  color: #c9d1d9;
  font-size: 12px;
  cursor: pointer;
}

.log-copy-btn:hover {
  background: #30363d;
}

.log-terminal {
  flex: 1;
  min-height: 0;
  padding: 4px;
}

.log-terminal :deep(.xterm) {
  height: 100%;
}

.log-terminal :deep(.xterm-viewport) {
  overflow-y: auto !important;
}
</style>
