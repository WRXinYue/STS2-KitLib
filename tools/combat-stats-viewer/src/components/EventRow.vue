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
  <li class="grid grid-cols-[4.5rem_1fr_auto] items-center gap-3 px-4 py-2.5">
    <Badge
      variant="outline"
      :class="cn('justify-center rounded-md px-1.5 py-0.5 text-[0.68rem] font-bold', kindBadgeClass(event.kind))"
    >
      {{ kindText }}
    </Badge>
    <div class="min-w-0">
      <span
        v-if="event.actorName"
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
      <span class="font-medium text-foreground">{{ displayPrimary }}</span>
      <span v-if="displaySecondary" class="ml-1.5 text-sm text-muted-foreground">
        → {{ displaySecondary }}
      </span>
    </div>
    <span
      v-if="amount"
      class="font-semibold tabular-nums text-primary"
    >
      {{ amount }}
    </span>
  </li>
</template>
