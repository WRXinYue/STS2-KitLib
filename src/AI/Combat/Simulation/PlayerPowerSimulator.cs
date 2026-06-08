using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat.Simulation;

/// <summary>Install and tick player buff powers during combat simulation.</summary>
internal static class PlayerPowerSimulator {
    public static bool InstallsKind(CardMechanicProfile profile, PlayerPowerEffectKind kind) =>
        profile.Installs(kind);

    public static bool InstallsInferno(CardMechanicProfile profile) =>
        profile.Installs(PlayerPowerEffectKind.InfernoRetaliate);

    public static PlayerBuffState ParseBuffsFromPowers(JsonArray? powers) {
        if (powers == null)
            return PlayerBuffState.Empty;

        var buffs = PlayerBuffState.Empty;
        foreach (var node in powers) {
            if (node is not JsonObject power) continue;
            var id = (power["modelId"]?.GetValue<string>()
                ?? power["id"]?.GetValue<string>() ?? "").ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(id)) continue;

            int amount = power["amount"]?.GetValue<int>() ?? 0;
            int selfDamage = power["selfDamage"]?.GetValue<int>() ?? 0;
            if (!PlayerPowerEffectIndex.TryResolvePowerId(id, out var kind))
                continue;

            buffs = MergeParsedPower(buffs, kind, amount, selfDamage);
        }

        return buffs;
    }

    static PlayerBuffState MergeParsedPower(
        PlayerBuffState buffs,
        PlayerPowerEffectKind kind,
        int amount,
        int selfDamage) {
        if (amount <= 0 && selfDamage <= 0)
            return buffs;

        return kind switch {
            PlayerPowerEffectKind.InfernoRetaliate => buffs with {
                InfernoRetaliation = Math.Max(buffs.InfernoRetaliation, amount),
                TurnStartSelfDamage = Math.Max(buffs.TurnStartSelfDamage, selfDamage > 0 ? selfDamage : amount > 0 ? 1 : 0),
            },
            PlayerPowerEffectKind.TurnStartBlock => buffs with {
                TurnStartBlock = Math.Max(buffs.TurnStartBlock, amount),
                TurnStartSelfDamage = Math.Max(buffs.TurnStartSelfDamage, selfDamage),
            },
            PlayerPowerEffectKind.TurnEndBlock => buffs with {
                TurnEndBlock = Math.Max(buffs.TurnEndBlock, amount),
            },
            _ => buffs,
        };
    }

    public static void InstallFromCard(
        CombatHandCard card,
        ref List<PlayerCombatModifier> modifiers,
        ref PlayerBuffState buffs) {
        if (!card.IsPower && card.Profile.PowerInstalls.Count == 0)
            return;

        foreach (var install in card.Profile.PowerInstalls)
            ApplyInstall(install, ref modifiers, ref buffs);
    }

    static void ApplyInstall(
        PlayerPowerInstall install,
        ref List<PlayerCombatModifier> modifiers,
        ref PlayerBuffState buffs) {
        switch (install.Kind) {
            case PlayerPowerEffectKind.Strength:
                modifiers.Add(PlayerCombatModifier.Strength(install.Amount));
                break;
            case PlayerPowerEffectKind.Dexterity:
                modifiers.Add(PlayerCombatModifier.Dexterity(install.Amount));
                break;
            case PlayerPowerEffectKind.Focus:
                modifiers.Add(PlayerCombatModifier.Focus(install.Amount));
                break;
            default:
                buffs = buffs.ApplyInstall(install);
                break;
        }
    }

    public static void ApplyPlayerHpLoss(
        int loss,
        PlayerBuffState buffs,
        ref int playerHp,
        ref List<CombatEnemy> enemies) {
        if (loss <= 0)
            return;

        playerHp = Math.Max(0, playerHp - loss);
        if (buffs.InfernoRetaliation > 0)
            CombatEffectApplier.ApplyAoeDamage(enemies, buffs.InfernoRetaliation);
    }

    public static void ApplyTurnEndBlock(PlayerBuffState buffs, ref int block) {
        if (buffs.TurnEndBlock > 0)
            block += buffs.TurnEndBlock;
    }

    public static void ApplyTurnStartPowers(
        PlayerBuffState buffs,
        ref int playerHp,
        ref int block,
        ref List<CombatEnemy> enemies) {
        if (buffs.TurnStartSelfDamage > 0)
            ApplyPlayerHpLoss(buffs.TurnStartSelfDamage, buffs, ref playerHp, ref enemies);

        if (buffs.TurnStartBlock > 0)
            block += buffs.TurnStartBlock;
    }
}
