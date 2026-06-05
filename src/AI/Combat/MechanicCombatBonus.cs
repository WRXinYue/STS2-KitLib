using System;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Combat.Simulation;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat;

/// <summary>Mechanic-index-driven combat bonuses (not per-card id tables).</summary>
internal static class MechanicCombatBonus {
    public static int Score(
        JsonObject snapshot,
        JsonObject card,
        CardMechanicProfile profile,
        JsonArray? hand,
        JsonObject? targetEnemy,
        int energy,
        bool suppressTransform = false) {
        var cardId = card["id"]?.GetValue<string>();
        if (CombatJunkCard.IsJunkId(cardId, card["rarity"]?.GetValue<string>())
            || string.Equals(card["cardType"]?.GetValue<string>(), "Status", StringComparison.OrdinalIgnoreCase)) {
            if (hand != null && HandHasEmergencyJunkClear(hand) && CardSelfExhausts(card))
                return HandHasAffordableAttack(hand) ? 8 : 14;
            return -200;
        }

        var bonus = 0;

        if (profile.Flags.HasFlag(CardMechanicFlags.TransformsHandAttacks)) {
            var attacks = CombatTransformSimulator.CountTransformableAttacks(hand);
            if (attacks == 0)
                return CombatScoreWeights.UnusableTransformPenalty;

            var skillCost = card["cost"]?.GetValue<int>() ?? 0;
            if (skillCost > energy)
                return CombatScoreWeights.UnusableTransformPenalty;

            var turnDelta = CombatTransformSimulator.EstimateTurnDamageDelta(hand, card, energy);
            if (turnDelta <= 0)
                return suppressTransform
                    ? -CombatScoreWeights.TransformThreatDiscountMax
                    : CombatScoreWeights.NegativeTransformPenalty;

            bonus += turnDelta;

            if (!suppressTransform) {
                if (profile.CanonicalCost <= 0)
                    bonus += CombatScoreWeights.FreeTransformBonus;
                if (attacks >= 2 && energy >= 2)
                    bonus += Math.Min(CombatScoreWeights.TurnOpenTransformBonus, turnDelta);
            }
        }
        else if (profile.Flags.HasFlag(CardMechanicFlags.TransformsCards)) {
            bonus += CombatTransformSimulator.EstimateDamageGain(hand, card) / 2;
        }

        if (profile.AppliedVulnerable > 0) {
            if (targetEnemy != null && EnemyTargetPriority.IsMinion(targetEnemy)
                && EnemyTargetPriority.HasAliveNonMinion(snapshot["combat"]?["enemies"]?.AsArray())) {
                bonus -= CombatScoreWeights.RedundantDebuffPenalty * 3;
            }
            else if (CombatPowerReader.GetVulnerable(targetEnemy) > 0) {
                bonus -= CombatScoreWeights.RedundantDebuffPenalty;
            }
            else if (hand != null) {
                var state = CombatState.FromSnapshot(snapshot);
                int handIndex = FindHandIndex(hand, card);
                if (handIndex >= 0) {
                    int setupBonus = 0;
                    if (targetEnemy != null) {
                        int enemyIndex = ResolveSetupEnemyIndex(
                            snapshot["combat"]?["enemies"]?.AsArray(), targetEnemy);
                        if (enemyIndex >= 0)
                            setupBonus = CombatSetupEvaluator.ComputeVulnerableSetupValue(state, handIndex, enemyIndex);
                    }
                    else {
                        foreach (var enemy in state.Enemies.Where(e => e.IsAlive && e.Vulnerable <= 0))
                            setupBonus = Math.Max(setupBonus,
                                CombatSetupEvaluator.ComputeVulnerableSetupValue(state, handIndex, enemy.Index));
                    }

                    bonus += setupBonus;
                }
            }
        }

        if (profile.AppliedWeak > 0) {
            var existing = CombatPowerReader.GetWeak(targetEnemy);
            bonus += existing <= 0
                ? CombatScoreWeights.WeakSetupBase + profile.AppliedWeak * CombatScoreWeights.WeakPerStack
                : -CombatScoreWeights.RedundantDebuffPenalty / 2;
        }

        bonus += ScorePileSkillBonus(profile, hand);

        return bonus;
    }

    static int ScorePileSkillBonus(CardMechanicProfile profile, JsonArray? hand) {
        int draw = CardPileEffectResolver.DrawCount(profile.Id);
        if (draw == 0 && profile.Flags.HasFlag(CardMechanicFlags.HasDraw))
            draw = 1;

        if (draw == 0 && !profile.Flags.HasFlag(CardMechanicFlags.HasExhaustFromHand))
            return 0;

        int junk = CountHandJunk(hand);
        int bonus = draw * 8;

        if (profile.Flags.HasFlag(CardMechanicFlags.HasExhaustFromHand)) {
            bonus += junk > 0
                ? junk * CombatJunkCard.DefaultJunkValue / 2
                : -10;
        }

        return bonus;
    }

    static int CountHandJunk(JsonArray? hand) {
        if (hand == null) return 0;
        int junk = 0;
        foreach (var node in hand) {
            if (node is not JsonObject card) continue;
            if (CombatJunkCard.IsJunkId(
                card["id"]?.GetValue<string>(),
                card["rarity"]?.GetValue<string>()))
                junk++;
        }
        return junk;
    }

    public static bool HandHasEmergencyJunkClearForScorer(JsonArray? hand, JsonObject card) =>
        HandHasEmergencyJunkClear(hand) && CardSelfExhausts(card);

    static bool HandHasEmergencyJunkClear(JsonArray? hand) {
        if (hand == null) return false;
        bool hasRelief = false;
        bool hasEmergency = false;
        foreach (var node in hand) {
            if (node is not JsonObject card) continue;
            var id = card["id"]?.GetValue<string>() ?? "";
            if (CardMechanicIndex.TryGet(id, out var profile)
                && (profile.Flags.HasFlag(CardMechanicFlags.HasExhaustFromHand)
                    || CardPileEffectResolver.ExhaustHandCount(id) > 0))
                hasRelief = true;
            if (CardSelfExhausts(card)
                && CombatJunkCard.IsJunkId(id, card["rarity"]?.GetValue<string>()))
                hasEmergency = true;
        }
        return !hasRelief && hasEmergency;
    }

    static bool CardSelfExhausts(JsonObject card) =>
        card["hasExhaust"]?.GetValue<bool>() == true
        || card["keywords"]?.AsArray()?.Any(k =>
            (k?.GetValue<string>() ?? "").Contains("Exhaust", StringComparison.OrdinalIgnoreCase)) == true;

    static bool HandHasAffordableAttack(JsonArray? hand) {
        if (hand == null) return false;
        foreach (var node in hand) {
            if (node is not JsonObject card) continue;
            var type = card["cardType"]?.GetValue<string>() ?? "";
            if (!type.Contains("Attack", StringComparison.OrdinalIgnoreCase)) continue;
            if ((card["damage"]?.GetValue<int>() ?? 0) <= 0) continue;
            if (card["canPlay"]?.GetValue<bool>() == false) continue;
            return true;
        }
        return false;
    }

    static int FindHandIndex(JsonArray hand, JsonObject card) {
        for (int i = 0; i < hand.Count; i++) {
            if (ReferenceEquals(hand[i], card))
                return i;
        }

        var cardId = card["id"]?.GetValue<string>();
        if (string.IsNullOrEmpty(cardId))
            return -1;

        for (int i = 0; i < hand.Count; i++) {
            if (hand[i] is JsonObject entry && entry["id"]?.GetValue<string>() == cardId)
                return i;
        }

        return -1;
    }

    static int ResolveSetupEnemyIndex(JsonArray? enemies, JsonObject targetEnemy) {
        if (enemies == null)
            return targetEnemy["index"]?.GetValue<int>() ?? -1;

        for (int i = 0; i < enemies.Count; i++) {
            if (enemies[i] is JsonObject enemy && ReferenceEquals(enemy, targetEnemy))
                return EnemyIndexResolver.CombatIndex(enemy, i);
        }

        return targetEnemy["index"]?.GetValue<int>() ?? -1;
    }

    public static bool IsSetupSkill(CardMechanicProfile profile) =>
        profile.Flags.HasFlag(CardMechanicFlags.TransformsHandAttacks)
        || profile.Flags.HasFlag(CardMechanicFlags.TransformsCards)
        || profile.Flags.HasFlag(CardMechanicFlags.AppliesVulnerable)
        || profile.Flags.HasFlag(CardMechanicFlags.AppliesWeak);
}

internal static class CombatEvalWeights {
    public const int MidTurnNetMultiplier = 6;
    public const int MidTurnLowHpNetBonus = 2;
    public const int TerminalHpMultiplier = 8;
    public const int TerminalNetPenalty = 12;
    public const int TerminalFullBlockBonus = 80;
    public const int TerminalBlockRewardPerPoint = 3;
    public const int BlockCoverPerPoint = 4;
    public const int LowHpNextTurnPenalty = 2;
    public const int UnusedEnergyExposedNetPenalty = 6;
    public const int UnsafeAttackPenaltyPerNet = 8;
    /// <summary>Ensures mid-turn combat wipe beats end-turn leaf ties in beam search.</summary>
    public const int CombatWipePriorityBonus = 500;
}

internal static class CombatScoreWeights {
    public const int FreeTransformBonus = 12;
    public const int TurnOpenTransformBonus = 40;
    public const int UnusableTransformPenalty = -200;
    public const int NegativeTransformPenalty = -80;
    public const int AttackBeforeTransformCap = 50;
    public const int TransformThreatDiscountMax = 20;
    public const int VulnerableSetupBase = 18;
    public const int VulnerablePerStack = 8;
    public const int WeakSetupBase = 10;
    public const int WeakPerStack = 5;
    public const int RedundantDebuffPenalty = 12;
    public const int NonSetupSkillPenalty = 40;
}
