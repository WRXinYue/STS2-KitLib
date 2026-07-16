export interface CombatStatEventDto {
  sequence: number;
  turn: number;
  kind: string;
  text: string;
  amount: number;
  actorKey: string;
  actorSide: string;
  actorName: string;
  statePhase?: string;
  creature?: CreatureStateDto | null;
  linkedToCardPlay?: boolean;
  sourceKind?: string;
  sourceKey?: string;
  sourceName?: string;
}

export interface PowerStateDto {
  id: string;
  displayName?: string;
  amount: number;
}

export interface CreatureStateDto {
  key: string;
  displayName: string;
  side: string;
  currentHp: number;
  maxHp: number;
  block: number;
  energy?: number | null;
  powers: PowerStateDto[];
  intentSummary?: string | null;
}

export interface TurnSnapshotDto {
  turn: number;
  phase: "start" | "end";
  creatures: CreatureStateDto[];
}

export interface PlayerCombatStatsDto {
  key: string;
  displayName: string;
  characterId: string;
  damageDealt: number;
  damageTaken: number;
  blockGained: number;
  cardsPlayed: number;
  hitCount: number;
  events: CombatStatEventDto[];
}

export interface CombatStatsSnapshotDto {
  encounterKey: string;
  isActive: boolean;
  maxTurn: number;
  players: PlayerCombatStatsDto[];
  combatEvents: CombatStatEventDto[];
  turnSnapshots: TurnSnapshotDto[];
  liveCreatures: CreatureStateDto[];
}

export interface CombatStatsLiveDto {
  active: CombatStatsSnapshotDto | null;
  isActive: boolean;
}

export type EventKindFilter = "all" | string;

export interface LogEntryDto {
  ts: number | string;
  lvl: string;
  text: string;
  mod?: string | null;
  scope?: string | null;
  boundary?: boolean;
}

export interface LogViewerFilterDto {
  minLevel?: string | null;
  textFilter?: string | null;
  hiddenSources?: string[] | null;
  loadedModIds?: string[] | null;
  modIdAliases?: Record<string, string> | null;
  suppressRules?: SuppressRuleDto[] | null;
}

export interface SuppressRuleDto {
  pattern: string;
  enabled: boolean;
}

export type LogWsServerMessage =
  | { type: "hello" }
  | { type: "log"; entry: LogEntryDto }
  | { type: "filter"; filter: LogViewerFilterDto | null };

export type WsClientMessage =
  | { type: "ping" }
  | { type: "requestStats" }
  | { type: "exportJson" };

export type WsServerMessage =
  | { type: "hello"; revision?: number }
  | { type: "pong" }
  | { type: "stats"; payload: CombatStatsLiveDto; revision?: number }
  | { type: "exported"; format: "json"; path: string };
