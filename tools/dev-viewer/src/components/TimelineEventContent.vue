<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import type { CombatStatEventDto } from "@/types";
import { formatAmount, splitEventText, eventSourceLabel, localizeEventText } from "@/lib/format";
import { kindBadgeClass } from "@/composables/useEventKind";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

const props = defineProps<{
  event: CombatStatEventDto;
  emphasized?: boolean;
}>();

const { t } = useI18n();

const parts = computed(() => splitEventText(props.event.text));
const sourceLabel = computed(() => eventSourceLabel(props.event));
const displayPrimary = computed(() => localizeEventText(parts.value.primary, t));
const displaySecondary = computed(() =>
  parts.value.secondary ? localizeEventText(parts.value.secondary, t) : "",
);

const kindText = computed(() => {
  const key = `kinds.${props.event.kind}`;
  const translated = t(key);
  return translated === key ? props.event.kind : translated;
});

const amount = computed(() => formatAmount(props.event.kind, props.event.amount));
</script>

<template>
  <div
    class="timeline-row"
    :class="emphasized ? 'timeline-row--emphasized' : undefined"
  >
    <Badge
      variant="outline"
      :class="cn(
        'shrink-0 justify-center rounded-md px-1.5 py-0.5 text-[0.68rem] font-bold',
        kindBadgeClass(event.kind),
        emphasized ? 'ring-1 ring-indigo-400/30' : '',
      )"
    >
      {{ kindText }}
    </Badge>
    <div class="timeline-row__main">
      <span
        v-if="event.actorName && event.actorName !== parts.primary && !parts.primary.startsWith(event.actorName)"
        class="mr-2 text-xs text-muted-foreground"
      >{{ event.actorName }}</span>
      <span
        v-if="sourceLabel"
        class="mr-1.5 text-sm font-medium text-sky-300/90"
      >{{ sourceLabel }}</span>
      <span
        v-if="sourceLabel"
        class="mr-1.5 text-muted-foreground"
      >→</span>
      <span
        class="text-foreground"
        :class="emphasized ? 'font-semibold' : 'font-medium'"
      >{{ displayPrimary }}</span>
      <span
        v-if="displaySecondary"
        class="ml-1.5 text-sm text-muted-foreground"
      >
        → {{ displaySecondary }}
      </span>
    </div>
    <span
      v-if="amount"
      class="timeline-row__amount"
    >
      {{ amount }}
    </span>
    <span
      v-else
      class="timeline-row__amount"
      aria-hidden="true"
    />
  </div>
</template>
