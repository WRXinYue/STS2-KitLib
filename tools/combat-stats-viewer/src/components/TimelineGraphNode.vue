<script setup lang="ts">
import type { GraphNode } from "@/lib/timeline-graph";
import GraphRow from "@/components/graph/GraphRow.vue";
import TimelineEventContent from "@/components/TimelineEventContent.vue";
import TimelineStateContent from "@/components/TimelineStateContent.vue";

defineProps<{
  node: GraphNode;
  spineDown?: boolean;
}>();
</script>

<template>
  <div
    v-if="node.type === 'play'"
    class="graph-play"
  >
    <GraphRow
      tone="play"
      :emphasized="true"
      :spine-down="node.children.length > 0 || !!spineDown"
    >
      <TimelineEventContent
        :event="node.event"
        emphasized
      />
    </GraphRow>

    <div
      v-if="node.children.length"
      class="graph-play__children"
    >
      <GraphRow
        v-for="(child, idx) in node.children"
        :key="`${node.event.sequence}-fx-${idx}`"
        branch
        :spine-down="idx < node.children.length - 1 || !!spineDown"
        :is-last="idx === node.children.length - 1"
        :tone="child.type === 'state' && child.phase === 'hit' ? 'hit' : 'neutral'"
      >
        <TimelineEventContent
          v-if="child.type === 'event'"
          :event="child.event"
        />
        <TimelineStateContent
          v-else-if="child.type === 'state'"
          :creature="child.event.creature!"
          :phase="child.phase"
        />
      </GraphRow>
    </div>
  </div>

  <GraphRow
    v-else-if="node.type === 'event'"
    tone="neutral"
    :spine-down="spineDown"
  >
    <TimelineEventContent :event="node.event" />
  </GraphRow>

  <GraphRow
    v-else
    :tone="node.phase === 'hit' ? 'hit' : node.phase === 'start' ? 'start' : 'end'"
    :spine-down="spineDown"
  >
    <TimelineStateContent
      :creature="node.event.creature!"
      :phase="node.phase"
    />
  </GraphRow>
</template>
