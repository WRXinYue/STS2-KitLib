using System.Collections.Generic;

namespace KitLib.AI.Knowledge;

public sealed record PotionCombatEffect(
    PotionCombatEffectKind Kind,
    int Amount = 0,
    string? Target = null);

public sealed record PotionRandomSpec(
    string Kind,
    string Pool,
    int PickCount = 1,
    int OfferCount = 3,
    int McSamples = 3);

public sealed record PotionCombatProfile(
    string Id,
    string TargetType,
    IReadOnlyList<PotionCombatEffect> Effects,
    PotionRandomSpec? Random,
    bool Simulatable);
