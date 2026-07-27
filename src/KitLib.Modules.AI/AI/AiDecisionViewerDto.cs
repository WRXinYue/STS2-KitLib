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
    IReadOnlyList<string> DecisionLog);

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
