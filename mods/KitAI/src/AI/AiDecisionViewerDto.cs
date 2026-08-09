using System.Collections.Generic;

namespace KitLib.AI;

/// <summary>JSON payload for KitLib Dev Viewer AI decision panel.</summary>
public sealed record AiDecisionLiveDto(
    AiDecisionSnapshotDto? Active,
    bool IsInCombat);

public sealed record AiDecisionSnapshotDto(
    string Phase,
    long UtcMs,
    AiTelemetryDto Telemetry,
    AiLastActionDto? LastAction,
    IReadOnlyList<AiHandCardDto> Hand,
    AiPileOutlookDto Piles,
    AiBlockPolicyDto BlockPolicy,
    IReadOnlyList<string> DecisionLog,
    IReadOnlyList<AiCardOfferDto> CardOffers,
    int SkipCost,
    AiFightOutlookDto? FightOutlook,
    AiMacroInsightsDto? MacroInsights,
    AiMapRouteInsightsDto? MapRouteInsights);

public sealed record AiMacroInsightsDto(
    AiMacroResourcesDto Resources,
    AiScoringPhaseDto PhaseWeights,
    AiDeckComboDto DeckCombo,
    string ScoringSummary);

public sealed record AiMacroResourcesDto(
    int Hp,
    int MaxHp,
    int Gold,
    int DeckSize,
    int ActIndex,
    int TotalFloor,
    int RouteFightScore,
    string PhaseLabel);

public sealed record AiScoringPhaseDto(
    float CurrentSimWeight,
    float OptionWeight,
    float DilutionWeight,
    string PhaseLabel,
    string Rationale);

public sealed record AiDeckComboDto(
    int RouteFightScore,
    int DeckQualityScore,
    int SurvivalGap,
    int ThinGap,
    int StarterBloat,
    IReadOnlyList<AiDeckArchetypeDto> Archetypes);

public sealed record AiDeckArchetypeDto(
    string Id,
    string Role,
    int DeckPieces,
    int RelicPieces,
    int ScoreContribution);

public sealed record AiCardOfferDto(
    int Index,
    string Id,
    string Name,
    int Total,
    int Marginal,
    int Synergy,
    int Option,
    int Dilution,
    int Early,
    float ExerciseProb,
    string PrimaryRole,
    bool FightFuture,
    string RoleReason,
    int InRunScore,
    int OutRunScore,
    IReadOnlyList<string> ArchetypeIds);

public sealed record AiFightOutlookDto(
    string EncounterId,
    int ExpectedRemainingHp,
    int MinRemainingHp,
    int ExpectedKillTurns,
    int ExpectedChip,
    int ExpectedFightChip,
    int LethalSamples,
    int SampleCount);

public sealed record AiTelemetryDto(
    string Summary,
    int PlayerHp,
    int PlayerMaxHp,
    int PlayerBlock,
    int Energy,
    int Incoming,
    int NetDamage,
    int NonDamageThreat,
    int NextTurnIncoming,
    int Junk,
    int Pollution,
    int PlayDamage,
    int PlayBlock,
    int SetupDebt,
    int InfernoDebt,
    string PeekSummary,
    int Outlook);

public sealed record AiLastActionDto(
    string ActionType,
    string Label,
    string Reason,
    int TargetIndex,
    int SecondaryIndex);

public sealed record AiHandCardDto(
    int Index,
    string Id,
    string Name,
    int Cost,
    int Damage,
    int Block,
    string CardType,
    bool CanPlay,
    int RankScore,
    bool BlockScaling,
    bool PureBlock,
    bool DeferBlockScaling,
    string DeferReason);

public sealed record AiPileOutlookDto(
    int DrawCount,
    int DiscardCount,
    int ExhaustCount,
    bool WillReshuffle,
    string PeekSummary);

public sealed record AiBlockPolicyDto(
    bool NeedsBlock,
    bool CanSkipBlockForKill,
    bool ShouldPrioritizeBlock,
    bool HasPureBlock,
    int EnergyReserve,
    int NetDamage,
    int AffordableBlock);

public sealed record AiMapRouteInsightsDto(
    string PathSummary,
    int PathScore,
    int PathRisk,
    float CombatsToRest,
    int ElitesToRest,
    string? NextNodeType,
    AiRestEvDto RestEv,
    IReadOnlyList<AiRouteFightEvDto> RouteFights,
    IReadOnlyList<AiMapOptionDto> MapOptions);

public sealed record AiRestEvDto(
    int HealEv,
    int SmithEv,
    int HealAmount,
    int RouteValueBaseline,
    int HealRouteValue,
    int SmithRouteValue,
    string Recommended,
    int? UpgradeCardIndex,
    string? UpgradeCardId);

public sealed record AiRouteFightEvDto(
    string EncounterId,
    string RoomType,
    float Weight,
    int RewardEv,
    int FightCost,
    int NetEv,
    int IncomingTurn1);

public sealed record AiMapOptionDto(
    int Index,
    string PointType,
    int Score,
    int Row,
    int Col);
