<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import type { CombatStatsSnapshotDto, CreatureStateDto } from "@/types";
import { buildTurnGraphs, eventSequence } from "@/lib/timeline-graph";
import GraphRow from "@/components/graph/GraphRow.vue";
import TimelineTurnSection from "@/components/TimelineTurnSection.vue";
import TimelineStateContent from "@/components/TimelineStateContent.vue";

const props = defineProps<{
  snapshot: CombatStatsSnapshotDto;
  search: string;
  kindFilter: string;
}>();

const { t } = useI18n();

const filteredEvents = computed(() => {
  const q = props.search.trim().toLowerCase();
  return props.snapshot.combatEvents.filter((ev) => {
    if (props.kindFilter !== "all" && ev.kind !== props.kindFilter)
      return false;
    if (!q)
      return true;
    const creatureName = ev.creature?.displayName ?? "";
    const hay = `${ev.kind} ${ev.text} ${ev.turn} ${ev.actorName} ${creatureName}`.toLowerCase();
    return hay.includes(q);
  });
});

const turnGroups = computed(() => {
  const map = new Map<number, typeof filteredEvents.value>();
  filteredEvents.value.forEach((ev, index) => {
    const list = map.get(ev.turn) ?? [];
    list.push({ ...ev, sequence: eventSequence(ev, index) });
    map.set(ev.turn, list);
  });
  return [...map.entries()].sort(([a], [b]) => a - b);
});

const turnGraphs = computed(() => buildTurnGraphs(turnGroups.value));

const combatEndItems = computed((): CreatureStateDto[] =>
  props.snapshot.liveCreatures
    .filter((c) => c.side === "player")
    .concat(props.snapshot.liveCreatures.filter((c) => c.side === "enemy")),
);
</script>

<template>
  <section
    v-if="!snapshot.isActive && combatEndItems.length"
    class="mb-10"
  >
    <div class="graph-section-divider">
      <div class="graph-section-divider__line" />
      <span class="graph-section-divider__label">
        {{ t("state.combatEnd") }}
      </span>
      <div class="graph-section-divider__line" />
    </div>

    <div class="rounded-lg border border-border/60 bg-card/40 px-1 py-1">
      <GraphRow
        v-for="(creature, idx) in combatEndItems"
        :key="`final-${creature.key}`"
        tone="final"
        :spine-down="idx < combatEndItems.length - 1"
      >
        <TimelineStateContent
          :creature="creature"
          phase="final"
        />
      </GraphRow>
    </div>
  </section>

  <p
    v-if="!turnGraphs.length"
    class="py-8 text-center text-sm text-muted-foreground"
  >
    {{ t("empty.noEvents") }}
  </p>

  <div
    v-else
    class="flex flex-col"
  >
    <TimelineTurnSection
      v-for="turnGraph in turnGraphs"
      :key="turnGraph.turn"
      :turn-graph="turnGraph"
    />
  </div>
</template>
