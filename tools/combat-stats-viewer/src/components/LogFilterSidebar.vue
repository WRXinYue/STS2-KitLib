<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import type { LogViewerFilterDto } from "../types";
import { modColorHex } from "../lib/log-mod-colors";
import {
  isSourceVisible,
  listModSources,
  shortRuleLabel,
} from "../lib/log-filter-state";

const props = defineProps<{
  filter: LogViewerFilterDto;
  syncWithGame: boolean;
  stats: {
    visibleCount: number;
    suppressedCount: number;
    bySource: Record<string, number>;
    ruleHits: Record<string, number>;
  };
}>();

const emit = defineEmits<{
  "update:syncWithGame": [value: boolean];
  updateFilter: [patch: Partial<LogViewerFilterDto>];
}>();

const { t } = useI18n();

const modSources = computed(() =>
  listModSources(props.filter, Object.keys(props.stats.bySource)),
);
const suppressRules = computed(() => props.filter.suppressRules ?? []);

const levelValue = computed(() => props.filter.minLevel ?? "all");

function setLevel(level: string | null) {
  emit("updateFilter", { minLevel: level });
}

function setTextFilter(text: string) {
  emit("updateFilter", { textFilter: text });
}

function toggleSource(source: string, visible: boolean) {
  const hidden = new Set(props.filter.hiddenSources ?? []);
  if (visible)
    hidden.delete(source);
  else
    hidden.add(source);
  emit("updateFilter", { hiddenSources: [...hidden] });
}

function toggleRule(pattern: string, enabled: boolean) {
  const rules = (props.filter.suppressRules ?? []).map((r) =>
    r.pattern === pattern ? { ...r, enabled } : r,
  );
  emit("updateFilter", { suppressRules: rules });
}

function sourceColor(source: string): string {
  if (source === "Game")
    return "#8b949e";
  const hex = modColorHex(source);
  return `#${hex}`;
}

const sortedSources = computed(() => {
  const entries = Object.entries(props.stats.bySource);
  entries.sort((a, b) => {
    if (a[0] === "Game")
      return -1;
    if (b[0] === "Game")
      return 1;
    return b[1] - a[1];
  });
  return entries;
});
</script>

<template>
  <aside class="log-sidebar">
    <label class="log-sync">
      <input
        type="checkbox"
        :checked="syncWithGame"
        @change="emit('update:syncWithGame', ($event.target as HTMLInputElement).checked)"
      >
      <span>{{ t("logs.syncInGame") }}</span>
    </label>

    <section class="log-section">
      <h3 class="log-section__title">{{ t("logs.filter.level") }}</h3>
      <div class="log-chips">
        <button
          type="button"
          class="log-chip"
          :class="{ 'log-chip--active': levelValue === 'all' }"
          :disabled="syncWithGame"
          @click="setLevel(null)"
        >
          {{ t("logs.filter.all") }}
        </button>
        <button
          type="button"
          class="log-chip"
          :class="{ 'log-chip--active': levelValue === 'info' }"
          :disabled="syncWithGame"
          @click="setLevel('info')"
        >
          {{ t("logs.filter.info") }}
        </button>
        <button
          type="button"
          class="log-chip"
          :class="{ 'log-chip--active': levelValue === 'warn' }"
          :disabled="syncWithGame"
          @click="setLevel('warn')"
        >
          {{ t("logs.filter.warn") }}
        </button>
        <button
          type="button"
          class="log-chip"
          :class="{ 'log-chip--active': levelValue === 'error' }"
          :disabled="syncWithGame"
          @click="setLevel('error')"
        >
          {{ t("logs.filter.error") }}
        </button>
      </div>
    </section>

    <section class="log-section">
      <h3 class="log-section__title">{{ t("logs.filter.text") }}</h3>
      <input
        class="log-search"
        type="search"
        :value="filter.textFilter ?? ''"
        :disabled="syncWithGame"
        :placeholder="t('logs.filter.textPlaceholder')"
        @input="setTextFilter(($event.target as HTMLInputElement).value)"
      >
    </section>

    <section
      v-if="modSources.length"
      class="log-section"
    >
      <h3 class="log-section__title">{{ t("logs.filter.mods") }}</h3>
      <div class="log-checks">
        <label
          v-for="source in modSources"
          :key="source"
          class="log-check"
        >
          <input
            type="checkbox"
            :checked="isSourceVisible(source, filter)"
            :disabled="syncWithGame"
            @change="toggleSource(source, ($event.target as HTMLInputElement).checked)"
          >
          <span :style="{ color: sourceColor(source) }">{{ source }}</span>
        </label>
      </div>
    </section>

    <section class="log-section">
      <h3 class="log-section__title">{{ t("logs.filter.suppress") }}</h3>
      <div class="log-checks log-checks--rules">
        <label
          v-for="rule in suppressRules"
          :key="rule.pattern"
          class="log-check"
          :title="rule.pattern"
        >
          <input
            type="checkbox"
            :checked="rule.enabled"
            :disabled="syncWithGame"
            @change="toggleRule(rule.pattern, ($event.target as HTMLInputElement).checked)"
          >
          <span>
            {{ shortRuleLabel(rule.pattern) }}
            <template v-if="stats.ruleHits[rule.pattern]">
              ({{ stats.ruleHits[rule.pattern] }})
            </template>
          </span>
        </label>
      </div>
    </section>

    <section class="log-section log-section--stats">
      <h3 class="log-section__title">{{ t("logs.stats.title") }}</h3>
      <p class="log-count">
        {{ stats.suppressedCount > 0
          ? t("logs.stats.countFiltered", { visible: stats.visibleCount, suppressed: stats.suppressedCount })
          : t("logs.stats.count", { n: stats.visibleCount }) }}
      </p>
      <ul
        v-if="sortedSources.length"
        class="log-stats"
      >
        <li
          v-for="[source, count] in sortedSources"
          :key="source"
          class="log-stats__row"
        >
          <span
            class="log-stats__name"
            :style="{ color: sourceColor(source) }"
          >{{ source }}</span>
          <span
            class="log-stats__count"
            :style="{ color: sourceColor(source) }"
          >{{ count }}</span>
        </li>
      </ul>
      <p
        v-else
        class="log-stats__empty"
      >
        {{ t("logs.stats.empty") }}
      </p>
    </section>
  </aside>
</template>

<style scoped>
.log-sidebar {
  width: 240px;
  flex-shrink: 0;
  overflow-y: auto;
  padding: 10px 12px;
  border-right: 1px solid #21262d;
  background: #0d1117;
  font-size: 12px;
}

.log-sync {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 12px;
  color: #c9d1d9;
  cursor: pointer;
}

.log-section {
  margin-bottom: 14px;
  padding-bottom: 12px;
  border-bottom: 1px solid #21262d;
}

.log-section--stats {
  border-bottom: none;
}

.log-section__title {
  margin: 0 0 8px;
  font-size: 11px;
  font-weight: 600;
  color: #58a6ff;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.log-chips {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.log-chip {
  padding: 5px 8px;
  text-align: left;
  border-radius: 6px;
  border: 1px solid transparent;
  background: transparent;
  color: #8b949e;
  cursor: pointer;
  font-size: 12px;
}

.log-chip:hover:not(:disabled) {
  background: #21262d;
  color: #c9d1d9;
}

.log-chip--active {
  background: #21262d;
  border-color: #30363d;
  color: #c9d1d9;
}

.log-chip:disabled {
  opacity: 0.75;
  cursor: default;
}

.log-search {
  width: 100%;
  box-sizing: border-box;
  padding: 6px 8px;
  border-radius: 6px;
  border: 1px solid #30363d;
  background: #161b22;
  color: #c9d1d9;
  font-size: 12px;
}

.log-search:disabled {
  opacity: 0.75;
}

.log-checks {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.log-checks--rules {
  max-height: 140px;
  overflow-y: auto;
}

.log-check {
  display: flex;
  gap: 6px;
  align-items: flex-start;
  color: #c9d1d9;
  cursor: pointer;
  line-height: 1.35;
}

.log-check input {
  margin-top: 2px;
  flex-shrink: 0;
}

.log-count {
  margin: 0 0 8px;
  color: #8b949e;
  font-size: 11px;
}

.log-stats {
  list-style: none;
  margin: 0;
  padding: 0;
}

.log-stats__row {
  display: flex;
  justify-content: space-between;
  gap: 8px;
  padding: 2px 0;
}

.log-stats__name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.log-stats__empty {
  margin: 0;
  color: #8b949e;
  font-size: 11px;
}
</style>
