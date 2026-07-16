import type { CombatStatEventDto } from "@/types";

export type StatePhase = "start" | "end" | "final" | "hit";

export type GraphNode =
  | { type: "event"; event: CombatStatEventDto }
  | { type: "state"; event: CombatStatEventDto; phase: StatePhase }
  | { type: "play"; event: CombatStatEventDto; children: GraphNode[] };

export interface TurnGraph {
  turn: number;
  nodes: GraphNode[];
  actionCount: number;
}

const PLAY_EFFECT_KINDS = new Set([
  "EnergySpent",
  "BlockGained",
  "DamageDealt",
  "DamageTaken",
  "DebuffApplied",
  "BuffApplied",
  "PowerSynergy",
]);

export function eventSequence(ev: CombatStatEventDto, index: number): number {
  return ev.sequence > 0 ? ev.sequence : index + 1;
}

export function sortEvents(events: CombatStatEventDto[]): CombatStatEventDto[] {
  return [...events]
    .map((ev, index) => ({ ...ev, sequence: eventSequence(ev, index) }))
    .sort((a, b) => a.sequence - b.sequence);
}

function isPlayEffect(ev: CombatStatEventDto): boolean {
  if (ev.kind === "CreatureState" && ev.statePhase === "hit")
    return true;
  return PLAY_EFFECT_KINDS.has(ev.kind);
}

function statePhase(ev: CombatStatEventDto): StatePhase {
  if (ev.statePhase === "hit")
    return "hit";
  if (ev.statePhase === "start")
    return "start";
  if (ev.statePhase === "end")
    return "end";
  if (ev.statePhase === "final")
    return "final";
  return "end";
}

function toLeaf(ev: CombatStatEventDto): GraphNode {
  if (ev.kind === "CreatureState" && ev.creature)
    return { type: "state", event: ev, phase: statePhase(ev) };
  return { type: "event", event: ev };
}

function isCardPlayEffect(ev: CombatStatEventDto): boolean {
  if (!isPlayEffect(ev))
    return false;
  return ev.linkedToCardPlay === true;
}

/** Groups pre-card effects under the following CardPlayed (history records the card last). */
export function buildTurnGraph(events: CombatStatEventDto[]): GraphNode[] {
  const sorted = sortEvents(events);
  const playIndices: number[] = [];
  sorted.forEach((ev, index) => {
    if (ev.kind === "CardPlayed")
      playIndices.push(index);
  });

  const nodes: GraphNode[] = [];
  let cursor = 0;

  for (const playIdx of playIndices) {
    const between = sorted.slice(cursor, playIdx);
    for (const ev of between) {
      if (!isPlayEffect(ev) || !isCardPlayEffect(ev))
        nodes.push(toLeaf(ev));
    }

    const effects = between.filter(isCardPlayEffect).map(toLeaf);
    nodes.push({
      type: "play",
      event: sorted[playIdx]!,
      children: effects,
    });
    cursor = playIdx + 1;
  }

  for (const ev of sorted.slice(cursor)) {
    if (ev.kind === "CardPlayed")
      continue;
    nodes.push(toLeaf(ev));
  }

  return nodes;
}

export function isStateEvent(ev: CombatStatEventDto): boolean {
  return ev.kind === "CreatureState" && ev.creature != null;
}

export function actionEventCount(events: CombatStatEventDto[]): number {
  return events.filter((ev) => !isStateEvent(ev)).length;
}

export function buildTurnGraphs(
  turnEntries: [number, CombatStatEventDto[]][],
): TurnGraph[] {
  return turnEntries.map(([turn, events]) => ({
    turn,
    nodes: buildTurnGraph(events),
    actionCount: actionEventCount(events),
  }));
}
