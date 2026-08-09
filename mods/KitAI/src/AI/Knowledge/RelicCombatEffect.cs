using System.Collections.Generic;

namespace KitLib.AI.Knowledge;

public sealed record RelicCombatEffect(
    RelicCombatEffectKind Kind,
    int Delta = 0,
    int Count = 0,
    int Block = 0,
    string? PowerId = null,
    int PowerAmount = 1,
    int? MaxCombatRound = null,
    int? MinCombatRound = null);

public sealed record RelicCombatProfile(
    string Id,
    IReadOnlyList<string> Hooks,
    IReadOnlyList<RelicCombatEffect> Effects,
    bool Simulatable,
    bool NeedsManual);
