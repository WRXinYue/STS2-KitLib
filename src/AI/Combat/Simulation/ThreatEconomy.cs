using System;
using System.Linq;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat.Simulation;

public static class ThreatEconomy {
    public static int TotalPressure(CombatState state) {
        var hp = ThreatModel.NetDamageAfterBlock(state) * 4;
        var next = ThreatModel.ScaledNextTurnPressure(state) * 2;
        var pollution = DeckPollutionEvaluator.ProjectedPollutionCost(state) / 2;
        var power = PowerDebuffCost(state);
        return hp + next + pollution + power;
    }

    public static int KillBeforeHitBonus(CombatEnemy target, CombatState state) {
        if (!target.IsAlive)
            return 0;

        int bonus = 0;
        var nextHit = ThreatModel.NextTurnAttackOn(target);
        if (ThreatModel.IncomingDamage(state) == 0 && nextHit >= 6)
            bonus += nextHit * 2;

        for (int i = 0; i < Math.Min(2, target.IntentSteps.Length); i++) {
            var step = target.IntentSteps[i];
            var effects = MoveEffectIndex.GetEffects(target.MonsterId, step.MoveId);
            foreach (var effect in effects) {
                if (effect.Kind == MonsterMoveEffectKind.StatusInject && effect.Count > 0)
                    bonus += effect.Count * 3;
                if (effect.Kind == MonsterMoveEffectKind.Summon)
                    bonus += 15;
            }
        }

        if (ThreatModel.IncomingDamage(state) == 0 && nextHit > 0)
            bonus += Math.Min(40, nextHit * 3);

        return bonus;
    }

    public static int ScaledNonDamagePressure(CombatState state) {
        var modeled = DeckPollutionEvaluator.ProjectedPollutionCost(state) / 3;
        if (modeled > 0)
            return modeled;

        return ThreatModel.ScaledNonDamagePressure(state);
    }

    static int PowerDebuffCost(CombatState state) {
        int cost = 0;
        foreach (var mod in state.Modifiers) {
            if (mod.AttackDamageMultiplier < 1f)
                cost += (int)Math.Round((1f - mod.AttackDamageMultiplier) * 30);
            cost += mod.SkillCostPenalty * 8;
            cost += mod.AttackCostPenalty * 8;
            cost += mod.BoundCardsPerTurn * 5;
        }

        return cost;
    }
}
