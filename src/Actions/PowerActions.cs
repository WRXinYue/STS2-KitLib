using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Actions;

internal static class PowerActions {
    public static IEnumerable<PowerModel> GetAllPowers() => ModelDb.AllPowers;

    public static Task AddPower(Player player, PowerModel power, int amount, PowerTarget target) =>
        AddPowerInternal(player, power, amount, target, mpSync: false);

    public static void RemovePower(Creature creature, PowerModel power) {
        if (MpCheatSession.InMultiplayerRun) {
            MainFile.Logger.Warn("PowerActions: Cannot remove power locally in multiplayer — use host power sync.");
            return;
        }
        RemovePowerInternal(creature, power);
    }

    public static void RemoveAllPowers(Creature creature) {
        if (MpCheatSession.InMultiplayerRun) {
            MainFile.Logger.Warn("PowerActions: Cannot clear powers locally in multiplayer — use host power sync.");
            return;
        }
        RemoveAllPowersInternal(creature);
    }

    public static string GetPowerDisplayName(PowerModel power) {
        try { return power.Title?.GetFormattedText() ?? ((AbstractModel)power).Id.Entry ?? "?"; }
        catch { return ((AbstractModel)power).Id.Entry ?? "?"; }
    }

    internal static PowerModel? FindPowerById(string powerId) {
        if (string.IsNullOrEmpty(powerId)) return null;
        return ModelDb.AllPowers.FirstOrDefault(p => ((AbstractModel)p).Id.Entry == powerId);
    }

    internal static bool TryValidateAddPower(MpCheatItemPayload payload, out string? error) {
        error = null;
        if (!CombatManager.Instance.IsInProgress) {
            error = I18N.T("mpcheat.power.notInCombat", "Powers only work during combat.");
            return false;
        }
        if (FindPowerById(payload.ItemId) == null) {
            error = "power not found";
            return false;
        }
        var player = CardActions.FindPlayerByNetId(payload.TargetPlayerNetId);
        if (player?.Creature == null) {
            error = "target player not found";
            return false;
        }
        if (payload.Amount < 1) {
            error = "invalid amount";
            return false;
        }
        return true;
    }

    internal static bool TryValidateRemovePower(MpCheatItemPayload payload, out string? error) {
        error = null;
        var player = CardActions.FindPlayerByNetId(payload.TargetPlayerNetId);
        if (player?.Creature == null) {
            error = "target player not found";
            return false;
        }
        if (FindPowerById(payload.ItemId) == null) {
            error = "power not found";
            return false;
        }
        var canonical = FindPowerById(payload.ItemId);
        var match = player.Creature.Powers.FirstOrDefault(p => p?.Id == canonical!.Id);
        if (match == null) {
            error = "power not on target";
            return false;
        }
        return true;
    }

    internal static bool TryValidateClearPowers(MpCheatItemPayload payload, out string? error) {
        error = null;
        var player = CardActions.FindPlayerByNetId(payload.TargetPlayerNetId);
        if (player?.Creature == null) {
            error = "target player not found";
            return false;
        }
        return true;
    }

    internal static Task ExecuteAddPowerFromMpSync(MpCheatItemPayload payload) {
        var player = CardActions.FindPlayerByNetId(payload.TargetPlayerNetId);
        var power = FindPowerById(payload.ItemId);
        if (player == null || power == null) return Task.CompletedTask;
        return AddPowerInternal(player, power, payload.Amount, (PowerTarget)payload.PowerTarget, mpSync: true);
    }

    internal static Task ExecuteRemovePowerFromMpSync(MpCheatItemPayload payload) {
        var player = CardActions.FindPlayerByNetId(payload.TargetPlayerNetId);
        var power = FindPowerById(payload.ItemId);
        if (player?.Creature == null || power == null) return Task.CompletedTask;
        RemovePowerInternal(player.Creature, power);
        return Task.CompletedTask;
    }

    internal static Task ExecuteClearPowersFromMpSync(MpCheatItemPayload payload) {
        var player = CardActions.FindPlayerByNetId(payload.TargetPlayerNetId);
        if (player?.Creature == null) return Task.CompletedTask;
        RemoveAllPowersInternal(player.Creature);
        return Task.CompletedTask;
    }

    private static async Task AddPowerInternal(Player player, PowerModel power, int amount, PowerTarget target, bool mpSync) {
        if (MpCheatSession.InMultiplayerRun && !mpSync) {
            MainFile.Logger.Warn("PowerActions: Cannot add power locally in multiplayer — use host power sync.");
            return;
        }

        if (!CombatManager.Instance.IsInProgress) {
            MainFile.Logger.Warn("[KitLib] AddPower: no active combat — powers require an in-progress combat session.");
            return;
        }

        switch (target) {
            case PowerTarget.Self:
                await ApplyPower(power, amount, player.Creature!, player.Creature!);
                break;

            case PowerTarget.AllEnemies: {
                var cs = CombatManager.Instance.DebugOnlyGetState();
                if (cs == null) return;
                foreach (var enemy in cs.Enemies.Where(e => e.IsAlive).ToArray())
                    await ApplyPower(power, amount, enemy, player.Creature!);
                break;
            }

            case PowerTarget.Allies: {
                var cs = CombatManager.Instance.DebugOnlyGetState();
                if (cs == null) return;
                foreach (var ally in cs.Allies.Where(c => c.IsAlive).ToArray())
                    await ApplyPower(power, amount, ally, player.Creature!);
                break;
            }

            case PowerTarget.SpecificTarget:
                await ApplyPower(power, amount, player.Creature!, player.Creature!);
                break;
        }
    }

    private static async Task ApplyPower(PowerModel power, int amount, Creature target, Creature source) {
        try {
            var mutable = power.ToMutable(0);
#if STS2_BETA
            await PowerCmd.Apply(new BlockingPlayerChoiceContext(), mutable, target, (decimal)amount, source, null, false);
#else
            await PowerCmd.Apply(mutable, target, (decimal)amount, source, null, false);
#endif
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[KitLib] ApplyPower failed ({((AbstractModel)power).Id.Entry}): {ex.Message}");
        }
    }

    private static void RemovePowerInternal(Creature creature, PowerModel power) {
        var match = creature.Powers.FirstOrDefault(p => p?.Id == power.Id);
        if (match != null)
            TaskHelper.RunSafely(PowerCmd.Remove(match));
    }

    private static void RemoveAllPowersInternal(Creature creature) {
        foreach (var p in creature.Powers.ToArray()) {
            if (p != null)
                TaskHelper.RunSafely(PowerCmd.Remove(p));
        }
    }
}
