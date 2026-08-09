using System;

namespace KitLib.AI.Combat.Simulation;

/// <summary>Aggregated player buff-power state not represented by flat combat modifiers.</summary>
public sealed record PlayerBuffState(
    int InfernoRetaliation = 0,
    int TurnStartSelfDamage = 0,
    int TurnStartBlock = 0,
    int TurnEndBlock = 0) {

    public static PlayerBuffState Empty => new();

    public PlayerBuffState ApplyInstall(PlayerPowerInstall install) {
        if (install.Amount <= 0 && install.SelfDamageIncrementOnPlay <= 0)
            return this;

        return install.Kind switch {
            PlayerPowerEffectKind.InfernoRetaliate => this with {
                InfernoRetaliation = InfernoRetaliation + Math.Max(0, install.Amount),
                TurnStartSelfDamage = TurnStartSelfDamage + Math.Max(0, install.SelfDamageIncrementOnPlay),
            },
            PlayerPowerEffectKind.TurnStartBlock => this with {
                TurnStartBlock = TurnStartBlock + Math.Max(0, install.Amount),
                TurnStartSelfDamage = TurnStartSelfDamage + Math.Max(0, install.SelfDamageIncrementOnPlay),
            },
            PlayerPowerEffectKind.TurnEndBlock => this with {
                TurnEndBlock = TurnEndBlock + Math.Max(0, install.Amount),
            },
            _ => this,
        };
    }
}
