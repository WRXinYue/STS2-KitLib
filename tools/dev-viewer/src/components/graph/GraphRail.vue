<script setup lang="ts">
export type GraphRailTone = "turn" | "play" | "neutral" | "hit" | "start" | "end" | "final";

withDefaults(
  defineProps<{
    tone?: GraphRailTone;
    /** Vertical spine continues through the full row height. */
    spineDown?: boolean;
    /** L-shaped connector for nested rows (e.g. card-play effects). */
    branch?: boolean;
    /** When branch=true, continue spine below the dot for non-last siblings. */
    isLast?: boolean;
  }>(),
  {
    tone: "neutral",
    spineDown: false,
    branch: false,
    isLast: true,
  },
);
</script>

<template>
  <div
    class="graph-rail"
    :class="[
      `graph-rail--${tone}`,
      spineDown && 'graph-rail--spine-down',
      branch && 'graph-rail--branch',
    ]"
    aria-hidden="true"
  >
    <span
      v-if="branch"
      class="graph-rail__elbow"
    />
    <span class="graph-rail__dot" />
    <span
      v-if="branch && spineDown && !isLast"
      class="graph-rail__tail"
    />
  </div>
</template>
