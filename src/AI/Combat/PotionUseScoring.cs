using System;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Core.Schema;
using KitLib.AI.Knowledge;
using KitLib.AI.Planning;

namespace KitLib.AI.Combat;

/// <summary>Shared potion combat-use scoring for beam QuickScore and PotionScorer fallback.</summary>
internal static class PotionUseScoring {
    public readonly record struct Context(
        float HpRatio,
        int Incoming,
        int NetAfterBlock,
        bool NeedsBlock,
        bool Fatal,
        bool CanLethal,
        bool CanSecureKill,
        int Energy,
        int MaxEnergy,
        int MaxOffense,
        int MinPlayableCost,
        int EnemyCount,
        int RetainScore,
        int PotionCount,
        bool BeltFull,
        bool IsBigFight,
        float AttackPlanWeight,
        float ScalingPlanWeight,
        float DrawPlanWeight,
        int LethalGap);

    public static Context FromState(CombatState state, string potionId) {
        var hpRatio = state.PlayerMaxHp > 0
            ? (float)state.PlayerHp / state.PlayerMaxHp
            : 1f;
        var incoming = ThreatModel.IncomingDamage(state);
        var netAfterBlock = ThreatModel.NetDamageAfterBlock(state);
        var maxOffense = SimLethalChecker.EstimateMaxDamage(state);

        return new Context(
            hpRatio,
            incoming,
            netAfterBlock,
            BlockDefensePolicy.NeedsBlock(state),
            ThreatModel.IsFatalIfUnblocked(state),
            SimLethalChecker.CanLethal(state, out _),
            BlockDefensePolicy.CanSkipBlockForKill(state),
            state.Energy,
            state.MaxEnergy,
            maxOffense,
            MinPlayableCost(state),
            state.AliveEnemyCount,
            PotionTierCatalog.GetRetainScore(potionId),
            state.Potions.Count,
            state.Potions.Count >= 3,
            false,
            1f,
            1f,
            1f,
            EstimateLethalGap(state, maxOffense));
    }

    public static Context FromSnapshot(JsonObject snapshot, string potionId, DeckPlan plan) {
        var hpRatio = IntentCalculator.HpRatio(snapshot);
        var incoming = IntentCalculator.TotalIncomingDamage(snapshot);
        var netAfterBlock = IntentCalculator.NetDamageAfterBlock(snapshot);
        var combat = snapshot["combat"]?.AsObject();
        var energy = combat?["currentEnergy"]?.GetValue<int>() ?? 0;
        var maxEnergy = combat?["maxEnergy"]?.GetValue<int>() ?? 3;
        var hand = combat?["hand"]?.AsArray();
        var canLethal = LethalChecker.CanLethal(snapshot, out _);
        var maxOffense = hand != null ? LethalChecker.EstimateMaxDamage(hand, energy, 0) : 0;
        var room = (snapshot["roomType"]?.GetValue<string>() ?? "").ToUpperInvariant();

        return new Context(
            hpRatio,
            incoming,
            netAfterBlock,
            IntentCalculator.NeedsBlock(snapshot),
            IntentCalculator.IsFatalIfUnblocked(snapshot),
            canLethal,
            BlockDefensePolicy.CanSkipBlockForKill(snapshot),
            energy,
            maxEnergy,
            maxOffense,
            MinPlayableCost(hand, energy),
            IntentCalculator.AliveEnemyCount(snapshot),
            PotionTierCatalog.GetRetainScore(potionId),
            snapshot["potions"]?.AsArray()?.Count ?? 0,
            snapshot["hasOpenPotionSlots"]?.GetValue<bool>() == false,
            room.Contains("ELITE") || room.Contains("BOSS"),
            plan.GetWeight(AiTag.Attack),
            plan.GetWeight(AiTag.Scaling),
            plan.GetWeight(AiTag.Draw),
            EstimateLethalGap(snapshot, maxOffense));
    }

    /// <summary>Energy potions are wasted when no hand card can use the gained energy this turn.</summary>
    public static bool IsEnergyPotionLowValue(CombatState state, PotionCombatProfile profile) {
        int gain = 0;
        foreach (var effect in profile.Effects) {
            if (effect.Kind == PotionCombatEffectKind.GainEnergy)
                gain += Math.Max(1, effect.Amount);
        }

        return gain > 0 && !EnergyGainEnablesPlay(state, gain);
    }

    public static bool EnergyGainEnablesPlay(CombatState state, int gain) {
        if (gain <= 0)
            return false;

        var withEnergy = state with { Energy = Math.Min(999, state.Energy + gain) };
        return CombatCardCost.HasAffordablePlay(withEnergy);
    }

    /// <summary>Weak/vulnerable potions are wasted when the current enemy intent is not an attack.</summary>
    public static bool IsAttackDebuffLowValue(CombatState state, PotionCombatProfile profile) {
        bool appliesDebuff = profile.Effects.Any(e =>
            e.Kind == PotionCombatEffectKind.ApplyWeak
            || e.Kind == PotionCombatEffectKind.ApplyVulnerable);
        if (!appliesDebuff)
            return false;
        return ThreatModel.IncomingDamage(state) <= 0;
    }

    public static int ScoreSimProfile(
        CombatState state,
        PotionCombatProfile profile,
        int enemyIndex,
        Context ctx) {
        if (profile.Random != null)
            return Math.Max(0, ScoreRandom(ctx) - WastePenalty(ctx));

        int score = 20;
        bool blockOnly = profile.Effects.Count > 0;
        bool hasDebuff = false;

        foreach (var effect in profile.Effects) {
            switch (effect.Kind) {
                case PotionCombatEffectKind.GainBlock:
                    score += ScoreBlockEffect(ctx, effect.Amount);
                    break;

                case PotionCombatEffectKind.GainEnergy:
                    blockOnly = false;
                    score += ScoreEnergyEffect(ctx, state, Math.Max(1, effect.Amount));
                    break;

                case PotionCombatEffectKind.DrawCards:
                    blockOnly = false;
                    score += ScoreDrawEffect(ctx, effect.Amount);
                    break;

                case PotionCombatEffectKind.GainStrength:
                case PotionCombatEffectKind.GainDexterity:
                    blockOnly = false;
                    score += ScoreBuffEffect(ctx, effect.Amount);
                    break;

                case PotionCombatEffectKind.ApplyWeak:
                    blockOnly = false;
                    hasDebuff = true;
                    score += ScoreWeakDebuff(ctx, state, enemyIndex, effect.Amount);
                    break;

                case PotionCombatEffectKind.ApplyVulnerable:
                    blockOnly = false;
                    hasDebuff = true;
                    score += ScoreDebuffEffect(ctx, effect.Amount, state, enemyIndex);
                    break;

                case PotionCombatEffectKind.DamageSingle:
                    blockOnly = false;
                    score += ScoreDamageSingle(state, ctx, effect.Amount, enemyIndex);
                    break;

                case PotionCombatEffectKind.DamageAll:
                    blockOnly = false;
                    score += effect.Amount * ctx.EnemyCount;
                    break;

                case PotionCombatEffectKind.GainHeal:
                    blockOnly = false;
                    score += ScoreHealEffect(ctx);
                    break;

                case PotionCombatEffectKind.GainMaxHp:
                    blockOnly = false;
                    score += ScoreGainMaxHp(ctx, effect.Amount);
                    break;

                case PotionCombatEffectKind.ApplyPoison:
                    blockOnly = false;
                    score += ScoreDamageSingle(state, ctx, effect.Amount, enemyIndex);
                    break;

                case PotionCombatEffectKind.GainFocus:
                case PotionCombatEffectKind.NextAttackDoubled:
                    blockOnly = false;
                    score += ScoreBuffEffect(ctx, effect.Amount > 0 ? effect.Amount : 2);
                    break;

                case PotionCombatEffectKind.GainStars:
                    blockOnly = false;
                    score += ScoreEnergyEffect(ctx);
                    break;

                case PotionCombatEffectKind.EnemyStrengthDown:
                    blockOnly = false;
                    score += effect.Amount * Math.Max(1, ctx.EnemyCount);
                    break;
            }
        }

        if (blockOnly && ctx.CanLethal && ctx.CanSecureKill)
            score = 4;

        if (hasDebuff && !blockOnly)
            score += DebuffDeferPenalty(ctx);

        score -= WastePenalty(ctx);
        return Math.Max(0, score);
    }

    public static int ScoreCategory(Context ctx, PotionMechanicProfile profile, PotionCategory category) {
        int score = 0;

        switch (category) {
            case PotionCategory.Heal:
                score += ScoreHealEffect(ctx);
                break;

            case PotionCategory.Block:
                score += ScoreBlockEffect(ctx, profile.EstimatedBlock);
                break;

            case PotionCategory.DamageSingle:
            case PotionCategory.DamageAoE:
                if (ctx.CanLethal) score += 15;
                if (ctx.EnemyCount >= 2 && category == PotionCategory.DamageAoE) score += 25;
                if (!ctx.CanLethal && ctx.MaxOffense > 0
                    && ctx.LethalGap > 0
                    && ctx.LethalGap <= profile.EstimatedDamage + 8)
                    score += 30;
                score += profile.EstimatedDamage;
                break;

            case PotionCategory.Energy:
                score += ScoreEnergyEffect(ctx);
                break;

            case PotionCategory.Draw:
                score += ScoreDrawEffect(ctx, 3);
                break;

            case PotionCategory.Buff:
                score += (int)Math.Round(ctx.AttackPlanWeight * 12f);
                if (ctx.EnemyCount >= 2) score += 8;
                score += ScoreBuffEffect(ctx, 2);
                break;

            case PotionCategory.Debuff:
                score += (int)Math.Round(ctx.AttackPlanWeight * 8f);
                if (ctx.EnemyCount >= 2) score += 10;
                if (ctx.Incoming >= 10) score += 12;
                score += DebuffDeferPenalty(ctx);
                break;

            case PotionCategory.Random:
                score += ScoreRandom(ctx);
                break;

            case PotionCategory.Utility:
                score += 5;
                break;
        }

        score += SynergyBonus(category, ctx);
        score -= WastePenalty(ctx);
        return Math.Max(0, score);
    }

    static int ScoreHealEffect(Context ctx) {
        if (ctx.HpRatio < 0.35f) return 40;
        if (ctx.HpRatio < 0.55f && ctx.Fatal) return 30;
        if (ctx.Fatal) return 20;
        return ScoreSlotClearBonus(ctx);
    }

    static int ScoreGainMaxHp(Context ctx, int amount) {
        int score = 12 + Math.Max(0, amount) * 2;
        score += ScoreSlotClearBonus(ctx);
        if (ctx.Incoming <= 0 && !ctx.NeedsBlock && ctx.HpRatio >= 0.5f)
            score += 10;
        return score;
    }

    public static int ScoreSlotClearBonus(Context ctx) {
        if (ctx.Fatal || ctx.NeedsBlock || ctx.Incoming > 0)
            return 0;
        if (!ctx.BeltFull && ctx.PotionCount < 3)
            return 0;
        if (ctx.RetainScore > 18)
            return 0;
        return 28 + (ctx.BeltFull ? 12 : 0);
    }

    static int ScoreBlockEffect(Context ctx, int amount) {
        if (ctx.CanLethal && ctx.CanSecureKill)
            return 4;

        int score = 0;
        if (ctx.Fatal)
            score += 45 + Math.Min(amount, ctx.Incoming) * 3;
        else if (ctx.NeedsBlock && ctx.Incoming >= 20)
            score += 30 + Math.Min(amount, ctx.Incoming) * 2;
        else if (ctx.NeedsBlock)
            score += 20 + Math.Min(amount, ctx.Incoming) * 2;
        return score;
    }

    static int ScoreEnergyEffect(Context ctx, CombatState? state = null, int gain = 2) {
        if (ctx.Energy >= ctx.MaxEnergy)
            return 0;
        if (state != null && !EnergyGainEnablesPlay(state, gain))
            return ScoreSlotClearBonus(ctx) > 0 ? ScoreSlotClearBonus(ctx) / 2 : 0;
        if (!ctx.CanLethal && ctx.MinPlayableCost > ctx.Energy && ctx.MinPlayableCost <= ctx.Energy + gain)
            return 35;
        if (ctx.CanLethal) return 10;
        return 20;
    }

    static int ScoreDrawEffect(Context ctx, int draw) {
        int score = 15 + draw * 4;
        if (ctx.NeedsBlock && ctx.Energy <= 1) score += 15;
        if (ctx.EnemyCount >= 2) score += 10;
        return score;
    }

    static int ScoreBuffEffect(Context ctx, int amount) {
        if (ctx.Energy >= ctx.MaxEnergy && !ctx.Fatal && ctx.NetAfterBlock <= 0)
            return -40 + amount * 6;
        if (ctx.Energy >= ctx.MaxEnergy - 1 && ctx.MaxOffense > 0 && ctx.NetAfterBlock <= 0)
            return -25 + amount * 6;
        return 30 + amount * 6;
    }

    static int ScoreDebuffEffect(Context ctx, int amount, CombatState state, int enemyIndex) {
        int score = 28 + amount * 6;
        if (enemyIndex >= 0) {
            var primary = CombatSetupEvaluator.PrimaryAttackTargetIndex(state);
            if (enemyIndex != primary)
                score -= 30;
        }
        return score;
    }

    static int ScoreWeakDebuff(Context ctx, CombatState state, int enemyIndex, int amount) {
        if (enemyIndex < 0)
            enemyIndex = CombatSetupEvaluator.PrimaryAttackTargetIndex(state);

        var target = state.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == enemyIndex);
        if (target == null)
            return 0;
        if (ctx.Incoming <= 0)
            return 0;
        if (target.Weak > 0)
            return Math.Max(0, amount * 2);

        int baseline = ThreatModel.ScheduledPressureScore(state);
        var enemies = state.Enemies.ToList();
        CombatEffectApplier.ApplyDebuff(enemies, enemyIndex, isAoe: false, "WEAK", amount);
        int saved = baseline - ThreatModel.ScheduledPressureScore(state.WithEnemies(enemies));

        int score = Math.Max(0, saved * 4 + amount * 3);

        if (enemyIndex != CombatSetupEvaluator.PrimaryAttackTargetIndex(state))
            score -= 25;

        if (ctx.Energy <= 0 && ctx.MaxOffense <= 0) {
            var afterTurn = CombatTurnResolver.ResolveEndTurn(state);
            int nextOffense = DeckPollutionEvaluator.ExpectedPlayableDamage(afterTurn);
            if (target.EffectiveHp <= nextOffense)
                score -= 55;
            else
                score -= 30;
        }

        if (baseline < 8 && ctx.Incoming <= 0)
            score -= 18;

        int attackPressure = ThreatModel.ScheduledAttackPressure(state);
        if (ctx.Incoming <= 0 && attackPressure < 12)
            score -= 40;

        if (ThreatModel.TotalNonDamageThreat(state) > attackPressure)
            score -= 25;

        return Math.Max(0, score);
    }

    static int ScoreDamageSingle(CombatState state, Context ctx, int amount, int enemyIndex) {
        int score = amount * 2;
        if (ctx.CanLethal) score += 15;
        if (enemyIndex >= 0) {
            var target = ResolveTarget(state, enemyIndex);
            if (target != null && amount >= target.EffectiveHp)
                score += 180;
        }
        return score;
    }

    static int ScoreRandom(Context ctx) {
        int score = 18;
        if (ctx.CanLethal || ctx.NeedsBlock) score += 8;
        if (ctx.Energy >= ctx.MaxEnergy && ctx.MaxOffense > 0 && ctx.NetAfterBlock <= 0)
            score -= 15;
        return score;
    }

    static int DebuffDeferPenalty(Context ctx) {
        if (!ctx.NeedsBlock && ctx.Incoming < 8 && ctx.MaxOffense > 0 && ctx.Energy >= 2)
            return -22;
        return 0;
    }

    static int SynergyBonus(PotionCategory category, Context ctx) => category switch {
        PotionCategory.Debuff when ctx.AttackPlanWeight > 0.9f => 8,
        PotionCategory.Buff when ctx.ScalingPlanWeight > 0.8f => 10,
        PotionCategory.Random when ctx.DrawPlanWeight > 0.7f => 6,
        _ => 0,
    };

    static int WastePenalty(Context ctx) {
        if (ctx.Fatal || ctx.NeedsBlock) return 0;
        if (ctx.Incoming <= 0 && (ctx.BeltFull || ctx.PotionCount >= 3) && ctx.RetainScore <= 18)
            return 0;
        if (!ctx.IsBigFight && ctx.HpRatio > 0.65f)
            return ctx.RetainScore + 15;
        if (!ctx.IsBigFight && ctx.HpRatio > 0.45f)
            return ctx.RetainScore / 2;
        return 0;
    }

    static CombatEnemy? ResolveTarget(CombatState state, int enemyIndex) {
        if (enemyIndex < 0 || enemyIndex >= state.Enemies.Count)
            return null;
        var enemy = state.Enemies[enemyIndex];
        return enemy.IsAlive ? enemy : null;
    }

    static int MinPlayableCost(CombatState state) {
        int min = 99;
        foreach (var card in state.Hand) {
            if (!CombatCardCost.CanAfford(card, state)) continue;
            int cost = CombatCardCost.EffectiveCost(card, state);
            if (cost < min) min = cost;
        }
        return min == 99 ? state.Energy + 1 : min;
    }

    static int MinPlayableCost(JsonArray? hand, int energy) {
        if (hand == null) return 99;
        int min = 99;
        foreach (var node in hand) {
            if (node is not JsonObject card) continue;
            if (card["canPlay"]?.GetValue<bool>() != true) continue;
            var cost = card["cost"]?.GetValue<int>() ?? 99;
            if (cost < min) min = cost;
        }
        return min == 99 ? energy + 1 : min;
    }

    static int EstimateLethalGap(CombatState state, int maxDamage) {
        int minHp = int.MaxValue;
        foreach (var enemy in state.Enemies) {
            if (!enemy.IsAlive) continue;
            minHp = Math.Min(minHp, enemy.EffectiveHp);
        }
        if (minHp == int.MaxValue) return 0;
        return Math.Max(0, minHp - maxDamage);
    }

    static int EstimateLethalGap(JsonObject snapshot, int maxDamage) {
        var combat = snapshot["combat"]?.AsObject();
        var enemies = combat?["enemies"]?.AsArray();
        if (enemies == null) return 0;

        int minHp = int.MaxValue;
        foreach (var node in enemies) {
            if (node is not JsonObject enemy) continue;
            if (enemy["isAlive"]?.GetValue<bool>() == false) continue;
            var hp = (enemy["currentHp"]?.GetValue<int>() ?? 0) + (enemy["block"]?.GetValue<int>() ?? 0);
            if (hp < minHp) minHp = hp;
        }
        if (minHp == int.MaxValue) return 0;
        return Math.Max(0, minHp - maxDamage);
    }
}
