<script setup lang="ts">
import { useI18n } from "vue-i18n";
import type { TurnGraph } from "@/lib/timeline-graph";
import GraphRail from "@/components/graph/GraphRail.vue";
import TimelineGraphNode from "@/components/TimelineGraphNode.vue";

defineProps<{
  turnGraph: TurnGraph;
}>();

const { t } = useI18n();
</script>

<template>
  <section class="graph-turn">
    <header class="graph-turn__header">
      <GraphRail
        tone="turn"
        :spine-down="turnGraph.nodes.length > 0"
      />
      <div class="graph-turn__meta">
        <h3 class="text-sm font-semibold text-primary">
          {{ t("timeline.turn", { n: turnGraph.turn }) }}
        </h3>
        <span class="text-xs text-muted-foreground">
          {{ t("timeline.eventCount", { n: turnGraph.actionCount }) }}
        </span>
      </div>
    </header>

    <div
      v-if="turnGraph.nodes.length"
      class="graph-turn__body"
    >
      <TimelineGraphNode
        v-for="(node, idx) in turnGraph.nodes"
        :key="`${turnGraph.turn}-${idx}-${node.type}`"
        :node="node"
        :spine-down="idx < turnGraph.nodes.length - 1"
      />
    </div>
  </section>
</template>
