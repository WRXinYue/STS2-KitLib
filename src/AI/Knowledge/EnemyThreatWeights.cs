using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace KitLib.AI.Knowledge;

/// <summary>Tunable non-damage threat weights (grid-search via tools/ai-bench).</summary>
public static class EnemyThreatWeights {
    public const int DebuffStrong = 8;
    public const int Debuff = 5;
    public const int Buff = 4;
    public const int Summon = 6;
    public const int Heal = 3;
    public const int CardDebuff = 7;
    public const double NextTurnUncertainMultiplier = 1.15;

    public static int IntentWeight(IntentType intent) => intent switch {
        IntentType.DebuffStrong => DebuffStrong,
        IntentType.Debuff => Debuff,
        IntentType.CardDebuff => CardDebuff,
        IntentType.Buff => Buff,
        IntentType.Summon => Summon,
        IntentType.Heal => Heal,
        _ => 0,
    };
}
