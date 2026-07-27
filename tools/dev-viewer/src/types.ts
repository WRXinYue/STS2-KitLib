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

export interface AiTelemetryDto {
  summary: string;
  playerHp: number;
  playerMaxHp: number;
  playerBlock: number;
  energy: number;
  incoming: number;
  netDamage: number;
  nonDamageThreat: number;
  nextTurnIncoming: number;
  junk: number;
  pollution: number;
  playDamage: number;
  playBlock: number;
  setupDebt: number;
  infernoDebt: number;
  peekSummary: string;
  outlook: number;
}

export interface AiLastActionDto {
  actionType: string;
  label: string;
  reason: string;
  targetIndex: number;
  secondaryIndex: number;
}

export interface AiHandCardDto {
  index: number;
  id: string;
  name: string;
  cost: number;
  damage: number;
  block: number;
  cardType: string;
  canPlay: boolean;
  rankScore: number;
  blockScaling: boolean;
  pureBlock: boolean;
  deferBlockScaling: boolean;
  deferReason: string;
}

export interface AiPileOutlookDto {
  drawCount: number;
  discardCount: number;
  exhaustCount: number;
  willReshuffle: boolean;
  peekSummary: string;
}

export interface AiBlockPolicyDto {
  needsBlock: boolean;
  canSkipBlockForKill: boolean;
  shouldPrioritizeBlock: boolean;
  hasPureBlock: boolean;
  energyReserve: number;
  netDamage: number;
  affordableBlock: number;
}

export interface AiDecisionSnapshotDto {
  phase: string;
  utcMs: number;
  telemetry: AiTelemetryDto;
  lastAction: AiLastActionDto | null;
  hand: AiHandCardDto[];
  piles: AiPileOutlookDto;
  blockPolicy: AiBlockPolicyDto;
  decisionLog: string[];
  cardOffers: AiCardOfferDto[];
  skipCost: number;
  fightOutlook: AiFightOutlookDto | null;
  macroInsights: AiMacroInsightsDto | null;
}

export interface AiMacroInsightsDto {
  resources: AiMacroResourcesDto;
  phaseWeights: AiScoringPhaseDto;
  deckCombo: AiDeckComboDto;
  scoringSummary: string;
}

export interface AiMacroResourcesDto {
  hp: number;
  maxHp: number;
  gold: number;
  deckSize: number;
  actIndex: number;
  totalFloor: number;
  routeFightScore: number;
  phaseLabel: string;
}

export interface AiScoringPhaseDto {
  currentSimWeight: number;
  optionWeight: number;
  dilutionWeight: number;
  phaseLabel: string;
  rationale: string;
}

export interface AiDeckComboDto {
  routeFightScore: number;
  deckQualityScore: number;
  survivalGap: number;
  thinGap: number;
  starterBloat: number;
  archetypes: AiDeckArchetypeDto[];
}

export interface AiDeckArchetypeDto {
  id: string;
  role: string;
  deckPieces: number;
  relicPieces: number;
  scoreContribution: number;
}

export interface AiCardOfferDto {
  index: number;
  id: string;
  name: string;
  total: number;
  marginal: number;
  synergy: number;
  option: number;
  dilution: number;
  early: number;
  exerciseProb: number;
  primaryRole: string;
  fightFuture: boolean;
  roleReason: string;
  inRunScore: number;
  outRunScore: number;
  archetypeIds: string[];
}

export interface AiFightOutlookDto {
  encounterId: string;
  expectedRemainingHp: number;
  minRemainingHp: number;
  expectedKillTurns: number;
  expectedChip: number;
  expectedFightChip: number;
  lethalSamples: number;
  sampleCount: number;
}

export interface AiDecisionLiveDto {
  active: AiDecisionSnapshotDto | null;
  isInCombat: boolean;
}

export type AiWsServerMessage =
  | { type: "hello"; stream?: string; revision?: number }
  | { type: "pong" }
  | { type: "ai"; payload: AiDecisionLiveDto; revision?: number };
