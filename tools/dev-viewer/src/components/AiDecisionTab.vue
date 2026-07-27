<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { useLiveAiDecision } from "@/composables/useLiveAiDecision";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";

const { t } = useI18n();
const { live, connected, isLiveHost } = useLiveAiDecision();

const snapshot = computed(() => live.value?.active ?? null);

const statusLine = computed(() => {
  if (!snapshot.value)
    return t("ai.status.noData");
  const label = live.value?.isInCombat ? t("ai.status.inCombat") : t("ai.status.outOfCombat");
  return `${label} · ${snapshot.value.phase} · ${snapshot.value.telemetry.summary}`;
});

const telemetryRows = computed(() => {
  const tel = snapshot.value?.telemetry;
  if (!tel)
    return [];
  return [
    { key: "hp", label: t("ai.telemetry.hp"), value: `${tel.playerHp}/${tel.playerMaxHp}` },
    { key: "block", label: t("ai.telemetry.block"), value: tel.playerBlock },
    { key: "energy", label: t("ai.telemetry.energy"), value: tel.energy },
    { key: "incoming", label: t("ai.telemetry.incoming"), value: tel.incoming },
    { key: "netDamage", label: t("ai.telemetry.netDamage"), value: tel.netDamage },
    { key: "nonDamageThreat", label: t("ai.telemetry.nonDamageThreat"), value: tel.nonDamageThreat },
    { key: "nextTurnIncoming", label: t("ai.telemetry.nextTurnIncoming"), value: tel.nextTurnIncoming },
    { key: "junk", label: t("ai.telemetry.junk"), value: tel.junk },
    { key: "pollution", label: t("ai.telemetry.pollution"), value: tel.pollution },
    { key: "playDamage", label: t("ai.telemetry.playDamage"), value: tel.playDamage },
    { key: "playBlock", label: t("ai.telemetry.playBlock"), value: tel.playBlock },
    { key: "setupDebt", label: t("ai.telemetry.setupDebt"), value: tel.setupDebt },
    { key: "infernoDebt", label: t("ai.telemetry.infernoDebt"), value: tel.infernoDebt },
    { key: "outlook", label: t("ai.telemetry.outlook"), value: tel.outlook },
    { key: "peek", label: t("ai.telemetry.peek"), value: tel.peekSummary || "—" },
  ];
});

const blockPolicyRows = computed(() => {
  const bp = snapshot.value?.blockPolicy;
  if (!bp)
    return [];
  return [
    { key: "needsBlock", label: t("ai.blockPolicy.needsBlock"), value: bp.needsBlock },
    { key: "canSkip", label: t("ai.blockPolicy.canSkipForKill"), value: bp.canSkipBlockForKill },
    { key: "prioritize", label: t("ai.blockPolicy.prioritize"), value: bp.shouldPrioritizeBlock },
    { key: "hasPure", label: t("ai.blockPolicy.hasPureBlock"), value: bp.hasPureBlock },
    { key: "reserve", label: t("ai.blockPolicy.energyReserve"), value: bp.energyReserve },
    { key: "net", label: t("ai.blockPolicy.netDamage"), value: bp.netDamage },
    { key: "affordable", label: t("ai.blockPolicy.affordableBlock"), value: bp.affordableBlock },
  ];
});

const cardOffers = computed(() => snapshot.value?.cardOffers ?? []);
const fightOutlook = computed(() => snapshot.value?.fightOutlook ?? null);
const macroInsights = computed(() => snapshot.value?.macroInsights ?? null);
const skipCost = computed(() => snapshot.value?.skipCost ?? 0);
const showMacroPanels = computed(() => !live.value?.isInCombat);
const macroTelemetryRows = computed(() =>
  telemetryRows.value.filter((row) => row.key === "hp"));

function roleLabel(role: string) {
  const key = `ai.roles.${role}`;
  const translated = t(key);
  return translated === key ? role : translated;
}

function boolLabel(v: boolean) {
  return v ? t("ai.bool.yes") : t("ai.bool.no");
}
</script>

<template>
  <div class="ai-tab">
    <p
      v-if="live"
      class="ai-tab__status"
    >
      {{ statusLine }}
      <span v-if="isLiveHost"> · {{ connected ? t("status.connected") : t("status.connecting") }}</span>
    </p>

    <Card
      v-if="!live?.active"
      class="ai-tab__empty"
    >
      <CardContent class="ai-tab__empty-content">
        <p class="text-muted-foreground">{{ t("ai.empty.noPayload") }}</p>
        <p class="mt-2 text-sm text-muted-foreground">
          {{ t("ai.empty.noPayloadHint") }}
        </p>
      </CardContent>
    </Card>

    <template v-else-if="snapshot">
      <Card v-if="snapshot.lastAction">
        <CardHeader class="pb-2">
          <CardTitle class="text-base">{{ t("ai.lastAction.title") }}</CardTitle>
        </CardHeader>
        <CardContent class="space-y-1 text-sm">
          <p>
            <span class="text-muted-foreground">{{ t("ai.lastAction.type") }}:</span>
            {{ snapshot.lastAction.actionType }}
          </p>
          <p>
            <span class="text-muted-foreground">{{ t("ai.lastAction.label") }}:</span>
            {{ snapshot.lastAction.label }}
          </p>
          <p v-if="snapshot.lastAction.reason">
            <span class="text-muted-foreground">{{ t("ai.lastAction.reason") }}:</span>
            {{ snapshot.lastAction.reason }}
          </p>
        </CardContent>
      </Card>

      <Card v-if="showMacroPanels && macroTelemetryRows.length > 0">
        <CardHeader class="pb-2">
          <CardTitle class="text-base">{{ t("ai.telemetry.title") }}</CardTitle>
        </CardHeader>
        <CardContent>
          <div class="ai-grid">
            <div
              v-for="row in macroTelemetryRows"
              :key="row.key"
              class="ai-grid__item"
            >
              <span class="ai-grid__label">{{ row.label }}</span>
              <span class="ai-grid__value">{{ row.value }}</span>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card v-if="showMacroPanels && macroInsights">
        <CardHeader class="pb-2">
          <CardTitle class="text-base">{{ t("ai.macro.resourcesTitle") }}</CardTitle>
        </CardHeader>
        <CardContent>
          <div class="ai-grid">
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.macro.hp") }}</span>
              <span class="ai-grid__value">
                {{ macroInsights.resources.hp }}/{{ macroInsights.resources.maxHp }}
              </span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.macro.gold") }}</span>
              <span class="ai-grid__value">{{ macroInsights.resources.gold }}</span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.macro.deckSize") }}</span>
              <span class="ai-grid__value">{{ macroInsights.resources.deckSize }}</span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.macro.act") }}</span>
              <span class="ai-grid__value">{{ macroInsights.resources.actIndex + 1 }}</span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.macro.floor") }}</span>
              <span class="ai-grid__value">{{ macroInsights.resources.totalFloor }}</span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.macro.routeFightScore") }}</span>
              <span class="ai-grid__value">{{ macroInsights.resources.routeFightScore }}</span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.macro.phase") }}</span>
              <span class="ai-grid__value">{{ macroInsights.resources.phaseLabel }}</span>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card v-if="showMacroPanels && macroInsights">
        <CardHeader class="pb-2">
          <CardTitle class="text-base">{{ t("ai.macro.phaseTitle") }}</CardTitle>
        </CardHeader>
        <CardContent class="space-y-2">
          <div class="ai-grid">
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.macro.simWeight") }}</span>
              <span class="ai-grid__value">{{ macroInsights.phaseWeights.currentSimWeight.toFixed(2) }}</span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.macro.optionWeight") }}</span>
              <span class="ai-grid__value">{{ macroInsights.phaseWeights.optionWeight.toFixed(2) }}</span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.macro.dilutionWeight") }}</span>
              <span class="ai-grid__value">{{ macroInsights.phaseWeights.dilutionWeight.toFixed(2) }}</span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.macro.phase") }}</span>
              <span class="ai-grid__value">{{ macroInsights.phaseWeights.phaseLabel }}</span>
            </div>
          </div>
          <p class="text-sm text-muted-foreground">
            {{ macroInsights.phaseWeights.rationale }}
          </p>
          <p class="text-xs text-muted-foreground">
            {{ t("ai.macro.summary") }}: {{ macroInsights.scoringSummary }}
          </p>
        </CardContent>
      </Card>

      <Card v-if="showMacroPanels && macroInsights">
        <CardHeader class="pb-2">
          <CardTitle class="text-base">{{ t("ai.macro.deckComboTitle") }}</CardTitle>
        </CardHeader>
        <CardContent class="ai-hand-wrap">
          <div class="ai-grid mb-3">
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.macro.routeFightScore") }}</span>
              <span class="ai-grid__value">{{ macroInsights.deckCombo.routeFightScore }}</span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.macro.deckQuality") }}</span>
              <span class="ai-grid__value">{{ macroInsights.deckCombo.deckQualityScore }}</span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.macro.survivalGap") }}</span>
              <span class="ai-grid__value">{{ macroInsights.deckCombo.survivalGap }}</span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.macro.thinGap") }}</span>
              <span class="ai-grid__value">{{ macroInsights.deckCombo.thinGap }}</span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.macro.starterBloat") }}</span>
              <span class="ai-grid__value">{{ macroInsights.deckCombo.starterBloat }}</span>
            </div>
          </div>
          <table
            v-if="macroInsights.deckCombo.archetypes.length > 0"
            class="ai-hand"
          >
            <thead>
              <tr>
                <th>{{ t("ai.macro.archetypeId") }}</th>
                <th>{{ t("ai.macro.archetypeRole") }}</th>
                <th>{{ t("ai.macro.deckPieces") }}</th>
                <th>{{ t("ai.macro.relicPieces") }}</th>
                <th>{{ t("ai.macro.contrib") }}</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="arch in macroInsights.deckCombo.archetypes"
                :key="arch.id"
              >
                <td>{{ arch.id }}</td>
                <td>{{ roleLabel(arch.role) }}</td>
                <td class="mono">{{ arch.deckPieces }}</td>
                <td class="mono">{{ arch.relicPieces }}</td>
                <td class="mono">{{ arch.scoreContribution }}</td>
              </tr>
            </tbody>
          </table>
          <p
            v-else
            class="text-sm text-muted-foreground"
          >
            {{ t("ai.log.empty") }}
          </p>
        </CardContent>
      </Card>

      <Card v-if="showMacroPanels && fightOutlook">
        <CardHeader class="pb-2">
          <CardTitle class="text-base">{{ t("ai.macro.inRunTitle") }} · {{ t("ai.fightOutlook.title") }}</CardTitle>
        </CardHeader>
        <CardContent>
          <div class="ai-grid">
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.fightOutlook.encounter") }}</span>
              <span class="ai-grid__value">{{ fightOutlook.encounterId }}</span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.fightOutlook.remainingHp") }}</span>
              <span class="ai-grid__value">{{ fightOutlook.expectedRemainingHp }}</span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.fightOutlook.minRemainingHp") }}</span>
              <span class="ai-grid__value">{{ fightOutlook.minRemainingHp }}</span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.fightOutlook.killTurns") }}</span>
              <span class="ai-grid__value">{{ fightOutlook.expectedKillTurns }}</span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.fightOutlook.chip") }}</span>
              <span class="ai-grid__value">{{ fightOutlook.expectedChip }}</span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.fightOutlook.fightChip") }}</span>
              <span class="ai-grid__value">{{ fightOutlook.expectedFightChip }}</span>
            </div>
            <div class="ai-grid__item">
              <span class="ai-grid__label">{{ t("ai.fightOutlook.lethal") }}</span>
              <span class="ai-grid__value">
                {{ fightOutlook.lethalSamples }}/{{ fightOutlook.sampleCount }}
              </span>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card v-if="showMacroPanels && cardOffers.length > 0">
        <CardHeader class="pb-2">
          <CardTitle class="text-base">{{ t("ai.cardOffers.title") }}</CardTitle>
        </CardHeader>
        <CardContent class="ai-hand-wrap">
          <p class="text-sm text-muted-foreground mb-2">
            {{ t("ai.cardOffers.skipCost") }}: {{ skipCost }}
          </p>
          <table class="ai-hand">
            <thead>
              <tr>
                <th>{{ t("ai.hand.name") }}</th>
                <th>{{ t("ai.cardOffers.role") }}</th>
                <th>{{ t("ai.cardOffers.total") }}</th>
                <th>{{ t("ai.cardOffers.inRun") }}</th>
                <th>{{ t("ai.cardOffers.outRun") }}</th>
                <th>{{ t("ai.cardOffers.marginal") }}</th>
                <th>{{ t("ai.cardOffers.option") }}</th>
                <th>{{ t("ai.cardOffers.synergy") }}</th>
                <th>{{ t("ai.cardOffers.dilution") }}</th>
                <th>{{ t("ai.cardOffers.early") }}</th>
                <th>{{ t("ai.cardOffers.exercise") }}</th>
                <th>{{ t("ai.cardOffers.reason") }}</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="offer in cardOffers"
                :key="offer.index"
              >
                <td>
                  <span class="font-medium">{{ offer.name }}</span>
                  <span class="text-xs text-muted-foreground ml-1">{{ offer.id }}</span>
                  <div
                    v-if="offer.archetypeIds?.length"
                    class="text-xs text-muted-foreground mt-1"
                  >
                    {{ offer.archetypeIds.join(", ") }}
                  </div>
                </td>
                <td class="ai-tags">
                  <Badge
                    :variant="offer.fightFuture ? 'destructive' : 'secondary'"
                  >
                    {{ roleLabel(offer.primaryRole) }}
                  </Badge>
                </td>
                <td class="mono">{{ offer.total }}</td>
                <td class="mono">{{ offer.inRunScore }}</td>
                <td class="mono">{{ offer.outRunScore }}</td>
                <td class="mono">{{ offer.marginal }}</td>
                <td class="mono">{{ offer.option }}</td>
                <td class="mono">{{ offer.synergy }}</td>
                <td class="mono">{{ offer.dilution }}</td>
                <td class="mono">{{ offer.early }}</td>
                <td class="mono">{{ (offer.exerciseProb * 100).toFixed(0) }}%</td>
                <td class="ai-defer text-xs text-muted-foreground">{{ offer.roleReason }}</td>
              </tr>
            </tbody>
          </table>
        </CardContent>
      </Card>

      <Card v-if="!showMacroPanels">
        <CardHeader class="pb-2">
          <CardTitle class="text-base">{{ t("ai.telemetry.title") }}</CardTitle>
        </CardHeader>
        <CardContent>
          <div class="ai-grid">
            <div
              v-for="row in telemetryRows"
              :key="row.key"
              class="ai-grid__item"
            >
              <span class="ai-grid__label">{{ row.label }}</span>
              <span class="ai-grid__value">{{ row.value }}</span>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card v-if="!showMacroPanels">
        <CardHeader class="pb-2">
          <CardTitle class="text-base">{{ t("ai.blockPolicy.title") }}</CardTitle>
        </CardHeader>
        <CardContent>
          <div class="ai-grid">
            <div
              v-for="row in blockPolicyRows"
              :key="row.key"
              class="ai-grid__item"
            >
              <span class="ai-grid__label">{{ row.label }}</span>
              <span class="ai-grid__value">{{ typeof row.value === "boolean" ? boolLabel(row.value) : row.value }}</span>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card v-if="!showMacroPanels">
        <CardHeader class="pb-2">
          <CardTitle class="text-base">{{ t("ai.piles.title") }}</CardTitle>
        </CardHeader>
        <CardContent class="space-y-1 text-sm">
          <p>{{ t("ai.piles.draw") }}: {{ snapshot.piles.drawCount }}</p>
          <p>{{ t("ai.piles.discard") }}: {{ snapshot.piles.discardCount }}</p>
          <p>{{ t("ai.piles.exhaust") }}: {{ snapshot.piles.exhaustCount }}</p>
          <p>{{ t("ai.piles.reshuffle") }}: {{ boolLabel(snapshot.piles.willReshuffle) }}</p>
          <p v-if="snapshot.piles.peekSummary">
            {{ t("ai.piles.peek") }}: {{ snapshot.piles.peekSummary }}
          </p>
        </CardContent>
      </Card>

      <Card v-if="!showMacroPanels && snapshot.hand.length > 0">
        <CardHeader class="pb-2">
          <CardTitle class="text-base">{{ t("ai.hand.title") }}</CardTitle>
        </CardHeader>
        <CardContent class="ai-hand-wrap">
          <table class="ai-hand">
            <thead>
              <tr>
                <th>{{ t("ai.hand.rank") }}</th>
                <th>{{ t("ai.hand.name") }}</th>
                <th>{{ t("ai.hand.cost") }}</th>
                <th>{{ t("ai.hand.dmg") }}</th>
                <th>{{ t("ai.hand.block") }}</th>
                <th>{{ t("ai.hand.play") }}</th>
                <th>{{ t("ai.hand.tags") }}</th>
                <th>{{ t("ai.hand.defer") }}</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="card in snapshot.hand"
                :key="card.index"
              >
                <td class="mono">{{ card.rankScore }}</td>
                <td>
                  <span class="font-medium">{{ card.name }}</span>
                  <span class="text-xs text-muted-foreground ml-1">{{ card.id }}</span>
                </td>
                <td class="mono">{{ card.cost }}</td>
                <td class="mono">{{ card.damage }}</td>
                <td class="mono">{{ card.block }}</td>
                <td>{{ card.canPlay ? t("ai.bool.yes") : t("ai.bool.no") }}</td>
                <td class="ai-tags">
                  <Badge
                    v-if="card.blockScaling"
                    variant="secondary"
                  >
                    {{ t("ai.hand.blockScaling") }}
                  </Badge>
                  <Badge
                    v-if="card.pureBlock"
                    variant="secondary"
                  >
                    {{ t("ai.hand.pureBlock") }}
                  </Badge>
                </td>
                <td class="ai-defer">
                  <template v-if="card.deferBlockScaling">
                    <Badge variant="destructive">{{ t("ai.hand.deferYes") }}</Badge>
                    <span
                      v-if="card.deferReason"
                      class="text-xs text-muted-foreground"
                    >{{ card.deferReason }}</span>
                  </template>
                  <span v-else class="text-muted-foreground">—</span>
                </td>
              </tr>
            </tbody>
          </table>
        </CardContent>
      </Card>

      <Card>
        <CardHeader class="pb-2">
          <CardTitle class="text-base">{{ t("ai.log.title") }}</CardTitle>
        </CardHeader>
        <CardContent>
          <pre
            v-if="snapshot.decisionLog.length"
            class="ai-log"
          >{{ snapshot.decisionLog.join("\n") }}</pre>
          <p
            v-else
            class="text-sm text-muted-foreground"
          >
            {{ t("ai.log.empty") }}
          </p>
        </CardContent>
      </Card>
    </template>
  </div>
</template>

<style scoped>
.ai-tab {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.ai-tab__status {
  font-size: 13px;
  color: #8b949e;
  margin: 0;
}

.ai-tab__empty-content {
  padding: 24px;
  text-align: center;
}

.ai-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
  gap: 8px 16px;
}

.ai-grid__item {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.ai-grid__label {
  font-size: 11px;
  color: #8b949e;
}

.ai-grid__value {
  font-size: 13px;
  font-variant-numeric: tabular-nums;
}

.ai-hand-wrap {
  overflow-x: auto;
}

.ai-hand {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}

.ai-hand th,
.ai-hand td {
  padding: 6px 8px;
  border-bottom: 1px solid #30363d;
  text-align: left;
  vertical-align: top;
}

.ai-hand th {
  color: #8b949e;
  font-weight: 500;
}

.mono {
  font-variant-numeric: tabular-nums;
}

.ai-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.ai-defer {
  display: flex;
  flex-direction: column;
  gap: 4px;
  max-width: 280px;
}

.ai-log {
  font-size: 11px;
  line-height: 1.45;
  max-height: 320px;
  overflow: auto;
  padding: 8px;
  background: #0d1117;
  border: 1px solid #30363d;
  border-radius: 6px;
  white-space: pre-wrap;
  word-break: break-word;
  margin: 0;
}
</style>
