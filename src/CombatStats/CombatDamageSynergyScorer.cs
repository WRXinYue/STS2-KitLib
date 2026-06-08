using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace KitLib.CombatStats;

/// <summary>
/// Estimates extra damage from Vulnerable (on target) and prevented damage from Weak (on attacker)
/// using the same power multipliers the game applies in combat.
/// </summary>
internal static class CombatDamageSynergyScorer {
    public readonly record struct SynergyHit(string PowerId, string Label, int Amount);

    public static IEnumerable<SynergyHit> AnalyzeDealDamage(
        Creature dealer,
        Creature receiver,
        DamageResult result,
        CardModel? cardSource) {
        if (!result.Props.IsPoweredAttack()) yield break;

        int total = result.UnblockedDamage + result.BlockedDamage + result.OverkillDamage;
        if (total <= 0) yield break;

        var vulnerable = receiver.GetPower<VulnerablePower>();
        if (vulnerable == null) yield break;

        decimal mult = vulnerable.ModifyDamageMultiplicative(receiver, 1m, result.Props, dealer, cardSource);
        if (mult <= 1.001m) yield break;

        int bonus = (int)Math.Round(total * (mult - 1m) / mult);
        if (bonus <= 0) yield break;

        yield return new SynergyHit(
            vulnerable.Id.Entry,
            I18N.T("combatStats.synergy.vulnerable", "Vulnerable bonus"),
            bonus);
    }

    public static IEnumerable<SynergyHit> AnalyzeDamageTaken(
        Creature? dealer,
        Creature receiver,
        DamageResult result,
        CardModel? cardSource) {
        if (dealer == null || !receiver.IsPlayer) yield break;
        if (!result.Props.IsPoweredAttack()) yield break;

        int taken = result.UnblockedDamage;
        if (taken <= 0) yield break;

        var weak = dealer.GetPower<WeakPower>();
        if (weak == null) yield break;

        decimal mult = weak.ModifyDamageMultiplicative(dealer, 1m, result.Props, dealer, cardSource);
        if (mult >= 0.999m || mult <= 0m) yield break;

        int saved = (int)Math.Round(taken * (1m / mult - 1m));
        if (saved <= 0) yield break;

        yield return new SynergyHit(
            weak.Id.Entry,
            I18N.T("combatStats.synergy.weak", "Weak mitigation"),
            saved);
    }
}
