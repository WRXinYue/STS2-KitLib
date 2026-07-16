<script setup lang="ts">
import { onMounted, onUnmounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import LangSwitcher from "@/components/LangSwitcher.vue";
import LogViewerPanel from "@/components/LogViewerPanel.vue";
import CombatStatsTab from "@/components/CombatStatsTab.vue";

type Tab = "logs" | "combat";

const { t } = useI18n();
const tab = ref<Tab>("logs");

function syncTabFromHash() {
  const raw = location.hash.replace(/^#\/?/, "");
  const path = raw.split("?")[0] || "";
  tab.value = path === "combat" ? "combat" : "logs";
}

function setTab(next: Tab) {
  tab.value = next;
  if (next === "logs") {
    const q = location.hash.includes("?") ? `?${location.hash.split("?").slice(1).join("?")}` : "";
    location.hash = `#/logs${q}`;
  }
  else {
    location.hash = "#/combat";
  }
}

onMounted(() => {
  syncTabFromHash();
  window.addEventListener("hashchange", syncTabFromHash);
});

onUnmounted(() => {
  window.removeEventListener("hashchange", syncTabFromHash);
});
</script>

<template>
  <div
    class="app-shell"
    :class="{ 'app-shell--fill': tab === 'logs' || tab === 'combat' }"
  >
    <header class="app-header app-content">
      <div class="min-w-0">
        <h1 class="text-xl font-semibold tracking-tight text-foreground md:text-2xl">
          {{ t("app.title") }}
        </h1>
        <p class="mt-1 text-sm text-muted-foreground">
          {{ t("app.subtitle") }}
        </p>
      </div>
      <LangSwitcher />
    </header>

    <nav class="app-nav app-content">
      <button
        type="button"
        class="app-nav-btn"
        :class="{ 'app-nav-btn--active': tab === 'logs' }"
        @click="setTab('logs')"
      >
        {{ t("nav.logs") }}
      </button>
      <button
        type="button"
        class="app-nav-btn"
        :class="{ 'app-nav-btn--active': tab === 'combat' }"
        @click="setTab('combat')"
      >
        {{ t("nav.combat") }}
      </button>
    </nav>

    <main class="app-main">
      <LogViewerPanel
        v-show="tab === 'logs'"
        :active="tab === 'logs'"
        class="tab-pane"
      />
      <div
        v-show="tab === 'combat'"
        class="combat-wrap tab-pane"
      >
        <div class="combat-inner app-content">
          <CombatStatsTab />
        </div>
      </div>
    </main>
  </div>
</template>

<style scoped>
.app-shell {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  background: var(--background, #0d1117);
}

.app-shell--fill {
  height: 100vh;
  overflow: hidden;
}

.app-content {
  width: 100%;
  max-width: 68rem;
  margin-inline: auto;
  padding-inline: clamp(16px, 3vw, 32px);
}

.app-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  padding-top: 16px;
  padding-bottom: 8px;
  flex-shrink: 0;
}

.app-nav {
  display: flex;
  gap: 4px;
  padding-bottom: 8px;
  flex-shrink: 0;
}

.app-nav-btn {
  padding: 6px 14px;
  font-size: 13px;
  border-radius: 6px;
  border: 1px solid transparent;
  color: #8b949e;
  background: transparent;
  cursor: pointer;
}

.app-nav-btn:hover {
  color: #c9d1d9;
  background: #21262d;
}

.app-nav-btn--active {
  color: #c9d1d9;
  background: #21262d;
  border-color: #30363d;
}

.app-main {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  position: relative;
}

.tab-pane {
  flex: 1;
  min-height: 0;
  width: 100%;
}

.combat-wrap {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
  width: 100%;
  overflow-y: auto;
}

.combat-inner {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
  padding-top: 12px;
  padding-bottom: 20px;
}
</style>
