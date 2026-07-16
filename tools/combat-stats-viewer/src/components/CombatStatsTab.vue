<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useLiveCombatStats } from "@/composables/useLiveCombatStats";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Card, CardContent } from "@/components/ui/card";
import TimelineView from "@/components/TimelineView.vue";

const { t } = useI18n();

const { live, connected, exportStatus, downloadJson, isLiveHost } = useLiveCombatStats();
const search = ref("");
const kindFilter = ref("all");
const exportError = ref("");

async function onExportJson() {
  exportError.value = "";
  exportStatus.value = "";
  try {
    await downloadJson();
  }
  catch {
    exportError.value = t("actions.exportFailed");
  }
}

const snapshot = computed(() => live.value?.active ?? null);

const statusLine = computed(() => {
  if (!snapshot.value)
    return t("status.noData");
  const enc = snapshot.value.encounterKey || "—";
  const label = live.value?.isActive ? t("status.live") : t("status.ended");
  return `${label} · ${enc} · ${t("status.turn", { n: snapshot.value.maxTurn })}`;
});

const kindOptions = computed(() => {
  const kinds = new Set<string>();
  for (const ev of snapshot.value?.combatEvents ?? [])
    kinds.add(ev.kind);
  return ["all", ...[...kinds].sort()];
});

function kindLabel(kind: string) {
  if (kind === "all")
    return t("filters.allKinds");
  const key = `kinds.${kind}`;
  const translated = t(key);
  return translated === key ? kind : translated;
}
</script>

<template>
  <div class="combat-tab">
    <p
      v-if="live"
      class="combat-tab__status"
    >
      {{ statusLine }}
      <span v-if="isLiveHost"> · {{ connected ? t("status.connected") : t("status.connecting") }}</span>
    </p>

    <div
      v-if="isLiveHost || live"
      class="combat-tab__toolbar"
    >
      <button
        type="button"
        class="inline-flex h-8 items-center rounded-md border border-border bg-secondary px-3 text-sm text-secondary-foreground hover:bg-secondary/80"
        @click="onExportJson"
      >
        {{ t("actions.exportJson") }}
      </button>
      <p
        v-if="exportStatus"
        class="self-center text-xs text-muted-foreground"
      >
        {{ t("actions.exported", { filename: exportStatus }) }}
      </p>
      <p
        v-if="exportError"
        class="self-center text-xs text-red-400"
      >
        {{ exportError }}
      </p>
    </div>

    <Card
      v-if="!live"
      class="combat-tab__empty"
    >
      <CardContent class="combat-tab__empty-content">
        <p class="text-muted-foreground">{{ t("empty.noPayload") }}</p>
        <p class="mt-2 text-sm text-muted-foreground">
          {{ t("empty.noPayloadHint", { cmd: "pnpm dev" }) }}
        </p>
      </CardContent>
    </Card>

    <template v-else-if="snapshot">
      <div class="combat-tab__filters">
        <Input
          v-model="search"
          type="search"
          class="bg-card"
          :placeholder="t('filters.search')"
        />
        <Select v-model="kindFilter">
          <SelectTrigger class="w-full bg-card sm:w-44">
            <SelectValue :placeholder="t('filters.allKinds')" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem
              v-for="k in kindOptions"
              :key="k"
              :value="k"
            >
              {{ kindLabel(k) }}
            </SelectItem>
          </SelectContent>
        </Select>
      </div>

      <div class="combat-tab__timeline">
        <TimelineView
          :snapshot="snapshot"
          :search="search"
          :kind-filter="kindFilter"
        />
      </div>
    </template>

    <Card
      v-else
      class="combat-tab__empty"
    >
      <CardContent class="combat-tab__empty-content text-muted-foreground">
        {{ t("empty.noPlayer") }}
      </CardContent>
    </Card>
  </div>
</template>

<style scoped>
.combat-tab {
  display: flex;
  flex: 1;
  flex-direction: column;
  width: 100%;
  min-height: 0;
  min-width: 0;
}

.combat-tab__status {
  margin-bottom: 12px;
  font-size: 12px;
  color: var(--muted-foreground, #8b949e);
  flex-shrink: 0;
}

.combat-tab__toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 12px;
  flex-shrink: 0;
}

.combat-tab__filters {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-bottom: 12px;
  flex-shrink: 0;
}

@media (min-width: 640px) {
  .combat-tab__filters {
    flex-direction: row;
  }
}

.combat-tab__timeline {
  flex: 1;
  min-height: 0;
  width: 100%;
}

.combat-tab__empty {
  flex: 1;
  display: flex;
  flex-direction: column;
  width: 100%;
  min-height: 240px;
}

.combat-tab__empty-content {
  display: flex;
  flex: 1;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 48px 24px;
  text-align: center;
}
</style>
