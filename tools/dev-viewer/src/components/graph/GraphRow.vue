<script setup lang="ts">
import GraphRail, { type GraphRailTone } from "@/components/graph/GraphRail.vue";

withDefaults(
  defineProps<{
    tone?: GraphRailTone;
    spineDown?: boolean;
    branch?: boolean;
    isLast?: boolean;
    emphasized?: boolean;
  }>(),
  {
    tone: "neutral",
    spineDown: false,
    branch: false,
    isLast: true,
    emphasized: false,
  },
);
</script>

<template>
  <div
    class="graph-row"
    :class="emphasized ? 'graph-row--emphasized' : undefined"
  >
    <GraphRail
      :tone="tone"
      :spine-down="spineDown"
      :branch="branch"
      :is-last="isLast"
    />
    <div class="graph-row__content">
      <slot />
    </div>
  </div>
</template>
