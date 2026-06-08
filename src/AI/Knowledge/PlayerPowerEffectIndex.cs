using System;
using System.Collections.Generic;
using KitLib.Actions;
using KitLib.AI.Combat.Simulation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.AI.Knowledge;

/// <summary>Maps official PowerVar types to combat-sim effect kinds (indexed from <see cref="ModelDb.AllCards"/>).</summary>
public static class PlayerPowerEffectIndex {
    static readonly Dictionary<string, (PlayerPowerEffectKind Kind, bool SelfDamageOnPlay)> ByPowerVarKey =
        new(StringComparer.OrdinalIgnoreCase) {
            ["StrengthPower"] = (PlayerPowerEffectKind.Strength, false),
            ["DexterityPower"] = (PlayerPowerEffectKind.Dexterity, false),
            ["FocusPower"] = (PlayerPowerEffectKind.Focus, false),
            ["InfernoPower"] = (PlayerPowerEffectKind.InfernoRetaliate, true),
            ["CrimsonMantlePower"] = (PlayerPowerEffectKind.TurnStartBlock, true),
            ["PlatingPower"] = (PlayerPowerEffectKind.TurnEndBlock, false),
        };

    public static IReadOnlyList<PlayerPowerInstall> ReadInstalls(CardModel card) {
        if (card.Type != CardType.Power)
            return [];

        var installs = new List<PlayerPowerInstall>();
        foreach (var key in CardEditActions.GetDynamicVarKeys(card)) {
            if (!TryResolveVarKey(key, out var kind, out var selfDamageOnPlay))
                continue;

            var amount = CardEditActions.GetDynamicVar(card, key);
            if (amount is not > 0)
                continue;

            installs.Add(new PlayerPowerInstall(
                kind,
                amount.Value,
                selfDamageOnPlay ? 1 : 0));
        }

        return installs;
    }

    public static IReadOnlyList<PlayerPowerInstall> ReadInstallsFromProfile(CardMechanicProfile profile) =>
        profile.PowerInstalls;

    public static bool TryResolvePowerId(string powerId, out PlayerPowerEffectKind kind) {
        kind = default;
        if (string.IsNullOrWhiteSpace(powerId))
            return false;

        var upper = powerId.ToUpperInvariant();

        if (upper.Contains("STRENGTH", StringComparison.Ordinal)) {
            kind = PlayerPowerEffectKind.Strength;
            return true;
        }

        if (upper.Contains("DEXTERITY", StringComparison.Ordinal)) {
            kind = PlayerPowerEffectKind.Dexterity;
            return true;
        }

        if (upper.Contains("FOCUS", StringComparison.Ordinal)) {
            kind = PlayerPowerEffectKind.Focus;
            return true;
        }

        if (upper.Contains("INFERNO", StringComparison.Ordinal)) {
            kind = PlayerPowerEffectKind.InfernoRetaliate;
            return true;
        }

        if (upper.Contains("CRIMSON", StringComparison.Ordinal) && upper.Contains("MANTLE", StringComparison.Ordinal)) {
            kind = PlayerPowerEffectKind.TurnStartBlock;
            return true;
        }

        if (upper.Contains("PLATING", StringComparison.Ordinal)) {
            kind = PlayerPowerEffectKind.TurnEndBlock;
            return true;
        }

        return false;
    }

    static bool TryResolveVarKey(string key, out PlayerPowerEffectKind kind, out bool selfDamageOnPlay) {
        kind = default;
        selfDamageOnPlay = false;
        if (ByPowerVarKey.TryGetValue(key, out var spec)) {
            kind = spec.Kind;
            selfDamageOnPlay = spec.SelfDamageOnPlay;
            return true;
        }

        if (key.Contains("Strength", StringComparison.OrdinalIgnoreCase)) {
            kind = PlayerPowerEffectKind.Strength;
            return true;
        }

        if (key.Contains("Dexterity", StringComparison.OrdinalIgnoreCase)) {
            kind = PlayerPowerEffectKind.Dexterity;
            return true;
        }

        if (key.Contains("Focus", StringComparison.OrdinalIgnoreCase)) {
            kind = PlayerPowerEffectKind.Focus;
            return true;
        }

        return false;
    }
}
