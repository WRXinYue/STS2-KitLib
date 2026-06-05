using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat.Simulation;

public static class CombatSimulator {
    public static CombatState Apply(CombatState state, SimCombatAction action) {
        if (action.Kind == SimActionKind.EndTurn)
            return state;

        if (action.HandIndex < 0 || action.HandIndex >= state.Hand.Count)
            return state;

        var card = state.Hand[action.HandIndex];
        if (!card.CanPlay || card.Cost > state.Energy)
            return state;

        var energy = state.Energy - card.Cost;
        var hand = state.Hand.ToList();
        var enemies = state.Enemies.ToList();
        var block = state.PlayerBlock;

        if (CombatTransformSimulator.IsHandAttackTransform(card.Profile)) {
            hand = ApplyHandTransform(hand, action.HandIndex);
            return state.WithPlayer(state.PlayerHp, block, energy).WithHand(hand);
        }

        hand.RemoveAt(action.HandIndex);

        if (card.IsAttack && card.Damage > 0) {
            if (card.IsAoe || card.TargetType is "AllEnemy")
                ApplyAoeDamage(enemies, card.Damage);
            else if (action.EnemyIndex >= 0)
                ApplySingleDamage(enemies, action.EnemyIndex, card.Damage);
        }

        if (card.Profile.AppliedVulnerable > 0) {
            ApplyDebuff(enemies, action, card.IsAoe, "VULNERABLE", card.Profile.AppliedVulnerable);
        }

        if (card.Profile.AppliedWeak > 0) {
            ApplyDebuff(enemies, action, card.IsAoe, "WEAK", card.Profile.AppliedWeak);
        }

        if (card.IsSkill) {
            if (card.Block > 0)
                block += card.Block;
            else if (!MechanicCombatBonus.IsSetupSkill(card.Profile))
                block += 5;
        }

        return state
            .WithPlayer(state.PlayerHp, block, energy)
            .WithHand(hand)
            .WithEnemies(enemies);
    }

    static List<CombatHandCard> ApplyHandTransform(List<CombatHandCard> hand, int skillIndex) {
        var skill = hand[skillIndex];
        var upgraded = (skill.ToJson()["upgradeLevel"]?.GetValue<int>() ?? 0) > 0;
        var rockDamage = CombatTransformSimulator.GiantRockDamage(upgraded);
        var result = new List<CombatHandCard>();

        for (int i = 0; i < hand.Count; i++) {
            if (i == skillIndex) {
                result.Add(hand[i]);
                continue;
            }

            var c = hand[i];
            if (!CombatTransformSimulator.IsTransformableAttack(c.ToJson()))
                result.Add(c);
            else
                result.Add(c with {
                    Id = "GIANT_ROCK",
                    Name = "Giant Rock",
                    Damage = rockDamage,
                    TargetType = "AnyEnemy",
                });
        }

        return result;
    }

    static void ApplyAoeDamage(List<CombatEnemy> enemies, int damage) {
        for (int i = 0; i < enemies.Count; i++) {
            if (!enemies[i].IsAlive) continue;
            var before = enemies[i];
            enemies[i] = ApplyDamageToEnemy(enemies[i], damage);
            if (before.IsAlive && !enemies[i].IsAlive && !before.IsMinion)
                ThreatModel.OnPrimaryEnemyKilled(enemies, i);
        }
    }

    static void ApplySingleDamage(List<CombatEnemy> enemies, int targetIndex, int damage) {
        for (int i = 0; i < enemies.Count; i++) {
            if (enemies[i].Index != targetIndex && i != targetIndex) continue;
            if (!enemies[i].IsAlive) continue;
            var before = enemies[i];
            enemies[i] = ApplyDamageToEnemy(enemies[i], damage);
            if (before.IsAlive && !enemies[i].IsAlive && !before.IsMinion)
                ThreatModel.OnPrimaryEnemyKilled(enemies, i);
            return;
        }
    }

    static CombatEnemy ApplyDamageToEnemy(CombatEnemy enemy, int damage) {
        var scaled = (int)Math.Round(damage * (enemy.Vulnerable > 0 ? 1.5f : 1f));
        var remaining = Math.Max(0, scaled - enemy.Block);
        var newBlock = Math.Max(0, enemy.Block - scaled);
        var newHp = Math.Max(0, enemy.CurrentHp - remaining);
        return enemy.WithHp(newHp, newBlock, newHp > 0);
    }

    static void ApplyDebuff(
        List<CombatEnemy> enemies,
        SimCombatAction action,
        bool isAoe,
        string token,
        int amount) {
        if (isAoe) {
            for (int i = 0; i < enemies.Count; i++) {
                if (!enemies[i].IsAlive) continue;
                enemies[i] = token == "VULNERABLE"
                    ? enemies[i].WithPowers(enemies[i].Vulnerable + amount, enemies[i].Weak)
                    : enemies[i].WithPowers(enemies[i].Vulnerable, enemies[i].Weak + amount);
            }
            return;
        }

        if (action.EnemyIndex < 0) return;
        for (int i = 0; i < enemies.Count; i++) {
            if (enemies[i].Index != action.EnemyIndex && i != action.EnemyIndex) continue;
            enemies[i] = token == "VULNERABLE"
                ? enemies[i].WithPowers(enemies[i].Vulnerable + amount, enemies[i].Weak)
                : enemies[i].WithPowers(enemies[i].Vulnerable, enemies[i].Weak + amount);
            return;
        }
    }

}
