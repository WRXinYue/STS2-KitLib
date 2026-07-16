<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import type { CreatureStateDto } from "@/types";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

const props = defineProps<{
  creature: CreatureStateDto;
  phase: "start" | "end" | "final" | "hit";
}>();

const { t } = useI18n();

const phaseLabel = computed(() => {
  if (props.phase === "start")
    return t("state.turnStart");
  if (props.phase === "hit")
    return t("state.afterHit");
  if (props.phase === "end")
    return t("state.turnEnd");
  return t("state.finalHp");
});

const sideLabel = computed(() =>
  props.creature.side === "player" ? t("state.player") : t("state.enemy"),
);

const hpText = computed(() =>
  t("state.hpShort", { current: props.creature.currentHp, max: props.creature.maxHp }),
);

const details = computed(() => {
  const parts: string[] = [];
  if (props.creature.block > 0)
    parts.push(t("state.block", { n: props.creature.block }));
  if (props.creature.energy != null)
    parts.push(t("state.energy", { n: props.creature.energy }));
  if (props.creature.intentSummary)
    parts.push(props.creature.intentSummary);
  if (props.creature.powers.length)
    parts.push(props.creature.powers.map((p) => `${p.displayName || p.id} ${p.amount}`).join(" · "));
  return parts.join(" · ");
});

const hpPercent = computed(() => {
  if (props.creature.maxHp <= 0)
    return 0;
  return Math.max(0, Math.min(100, Math.round((props.creature.currentHp / props.creature.maxHp) * 100)));
});
</script>

<template>
  <li class="grid grid-cols-[4.5rem_1fr_auto] items-center gap-3 px-4 py-2.5">
    <Badge
      variant="outline"
      :class="cn(
        'justify-center rounded-md px-1.5 py-0.5 text-[0.68rem] font-bold',
        creature.side === 'player'
          ? 'border-emerald-500/40 bg-emerald-500/10 text-emerald-400'
          : 'border-amber-500/40 bg-amber-500/10 text-amber-400',
      )"
    >
      {{ sideLabel }}
    </Badge>
    <div class="min-w-0">
      <div class="flex flex-wrap items-center gap-x-2 gap-y-0.5">
        <span class="text-[0.65rem] uppercase tracking-wide text-muted-foreground">
          {{ phaseLabel }}
        </span>
        <span class="font-medium text-foreground">
          {{ creature.displayName || creature.key }}
        </span>
        <Badge
          v-if="creature.currentHp <= 0 && creature.side === 'enemy'"
          variant="outline"
          class="text-[0.6rem]"
        >
          {{ t("state.defeated") }}
        </Badge>
      </div>
      <div class="mt-1 flex items-center gap-2">
        <div class="h-1 min-w-0 flex-1 overflow-hidden rounded-full bg-muted">
          <div
            class="h-full rounded-full transition-all"
            :class="creature.currentHp <= 0 ? 'bg-zinc-500/50' : creature.side === 'player' ? 'bg-emerald-500/80' : 'bg-red-500/75'"
            :style="{ width: `${hpPercent}%` }"
          />
        </div>
      </div>
      <p
        v-if="details"
        class="mt-1 text-xs text-muted-foreground"
      >
        {{ details }}
      </p>
    </div>
    <span class="shrink-0 text-right text-xs font-semibold tabular-nums text-primary">
      {{ hpText }}
    </span>
  </li>
</template>
