using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using KitLib.Multiplayer.Cheat;

namespace KitLib;

/// <summary>Immediate and per-frame effects for Harmony-backed player resource cheats.</summary>
internal static class PlayerCheatEffects {
    private const int ResourceAmount = 999;

    public static void ApplyImmediateIfEnabled(Player? player = null) {
        if (!MpCheatApplier.CheatsActive) return;
        if (!TryResolvePlayer(player, out var resolved) || resolved == null) return;
        ApplyForPlayer(resolved);
    }

    public static void Update() {
        if (!MpCheatApplier.CheatsActive) return;
        if (!RunManager.Instance.IsInProgress) return;
        if (!RunContext.TryGetRunAndPlayer(out _, out var player)) return;
        ApplyForPlayer(player);
    }

    private static void ApplyForPlayer(Player player) {
        var pcs = player.PlayerCombatState;
        if (pcs == null) return;

        if (MpCheatApplier.InfiniteEnergy(pcs) && pcs.Energy < ResourceAmount)
            pcs.Energy = ResourceAmount;

        if (MpCheatApplier.InfiniteStars(pcs) && pcs.Stars < ResourceAmount)
            pcs.Stars = ResourceAmount;
    }

    private static bool TryResolvePlayer(Player? player, out Player? resolved) {
        if (player != null) {
            resolved = player;
            return true;
        }
        return RunContext.TryGetRunAndPlayer(out _, out resolved);
    }
}
