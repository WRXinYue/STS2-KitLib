using System;
using System.Threading.Tasks;
using HarmonyLib;
using KitLib.Cheat;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.Patches;

static class StatLock {
    internal static bool Active =>
        MpCheatApplier.CheatsActive && !MpCheatSession.InMultiplayerRun;

    internal static bool IsLocal(Player player) =>
        RunContext.TryGetRunAndPlayer(out _, out var local)
        && local != null
        && player.NetId == local.NetId;

    internal static RuntimeStatModifiers? Mods => CheatRunState.StatModifiers;
}

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.GainGold))]
[HarmonyPriority(Priority.Low)]
public static class GoldLockGainPatch {
    public static void Prefix(Player player, ref decimal amount) {
        if (!IsGoldLocked(player)) return;
        amount = 0;
    }

    static bool IsGoldLocked(Player player) {
        if (!StatLock.Active || !StatLock.IsLocal(player)) return false;
        return StatLock.Mods is { LockGold: true };
    }
}

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.LoseGold))]
public static class GoldLockLosePatch {
    public static bool Prefix(Player player, ref Task __result) {
        if (!StatLock.Active || !StatLock.IsLocal(player)) return true;
        if (StatLock.Mods is not { LockGold: true }) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.CurrentHp), MethodType.Setter)]
public static class CurrentHpLockPatch {
    public static void Prefix(Creature __instance, ref int value) {
        if (__instance.Player is not { } player || !StatLock.Active || !StatLock.IsLocal(player)) return;
        var m = StatLock.Mods;
        if (m is not { LockCurrentHp: true }) return;
        int max = m.LockMaxHp ? Math.Max(1, m.LockedMaxHpValue) : __instance.MaxHp;
        value = Math.Clamp(m.LockedCurrentHpValue, 1, Math.Max(1, max));
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.MaxHp), MethodType.Setter)]
public static class MaxHpLockPatch {
    public static void Prefix(Creature __instance, ref int value) {
        if (__instance.Player is not { } player || !StatLock.Active || !StatLock.IsLocal(player)) return;
        var m = StatLock.Mods;
        if (m is not { LockMaxHp: true }) return;
        value = Math.Max(1, m.LockedMaxHpValue);
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.MaxEnergy), MethodType.Setter)]
public static class MaxEnergyLockPatch {
    public static void Prefix(Player __instance, ref int value) {
        if (!StatLock.Active || !StatLock.IsLocal(__instance)) return;
        var m = StatLock.Mods;
        if (m is not { LockMaxEnergy: true }) return;
        value = Math.Max(1, m.LockedMaxEnergyValue);
    }
}

[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.Energy), MethodType.Setter)]
public static class EnergyLockPatch {
    static readonly AccessTools.FieldRef<PlayerCombatState, Player> PlayerRef =
        AccessTools.FieldRefAccess<PlayerCombatState, Player>("_player");

    public static void Prefix(PlayerCombatState __instance, ref int value) {
        if (!CombatStatLock.TryGetLockedEnergy(__instance, out var locked)) return;
        value = locked;
    }
}

[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.LoseEnergy))]
public static class EnergyLockLosePatch {
    public static bool Prefix(PlayerCombatState __instance) =>
        !CombatStatLock.IsCurrentEnergyLocked(__instance);
}

[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.GainEnergy))]
public static class EnergyLockGainPatch {
    public static bool Prefix(PlayerCombatState __instance) =>
        !CombatStatLock.IsCurrentEnergyLocked(__instance);
}

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.LoseEnergy))]
public static class EnergyLockLoseCmdPatch {
    public static bool Prefix(Player player, ref Task __result) {
        if (player.PlayerCombatState == null || !CombatStatLock.IsCurrentEnergyLocked(player.PlayerCombatState))
            return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.GainEnergy))]
public static class EnergyLockGainCmdPatch {
    public static void Prefix(Player player, ref decimal amount) {
        if (player.PlayerCombatState != null && CombatStatLock.IsCurrentEnergyLocked(player.PlayerCombatState))
            amount = 0;
    }
}

[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.Stars), MethodType.Setter)]
public static class StarsLockPatch {
    static readonly AccessTools.FieldRef<PlayerCombatState, Player> PlayerRef =
        AccessTools.FieldRefAccess<PlayerCombatState, Player>("_player");

    public static void Prefix(PlayerCombatState __instance, ref int value) {
        if (!CombatStatLock.TryGetLockedStars(__instance, out var locked)) return;
        value = locked;
    }
}

[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.LoseStars))]
public static class StarsLockLosePatch {
    public static bool Prefix(PlayerCombatState __instance) =>
        !CombatStatLock.IsStarsLocked(__instance);
}

[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.GainStars))]
public static class StarsLockGainPatch {
    public static bool Prefix(PlayerCombatState __instance) =>
        !CombatStatLock.IsStarsLocked(__instance);
}

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.LoseStars))]
public static class StarsLockLoseCmdPatch {
    public static bool Prefix(Player player, ref Task __result) {
        if (player.PlayerCombatState == null || !CombatStatLock.IsStarsLocked(player.PlayerCombatState))
            return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.GainStars))]
public static class StarsLockGainCmdPatch {
    public static void Prefix(Player player, ref decimal amount) {
        if (player.PlayerCombatState != null && CombatStatLock.IsStarsLocked(player.PlayerCombatState))
            amount = 0;
    }
}

static class CombatStatLock {
    static readonly AccessTools.FieldRef<PlayerCombatState, Player> PlayerRef =
        AccessTools.FieldRefAccess<PlayerCombatState, Player>("_player");

    internal static bool IsCurrentEnergyLocked(PlayerCombatState pcs) {
        var player = PlayerRef(pcs);
        if (!StatLock.Active || !StatLock.IsLocal(player)) return false;
        return StatLock.Mods is { LockCurrentEnergy: true };
    }

    internal static bool TryGetLockedEnergy(PlayerCombatState pcs, out int locked) {
        locked = 0;
        if (!IsCurrentEnergyLocked(pcs)) return false;
        var m = StatLock.Mods!;
        var player = PlayerRef(pcs);
        int cap = m.LockMaxEnergy ? Math.Max(1, m.LockedMaxEnergyValue) : player.MaxEnergy;
        locked = Math.Clamp(m.LockedCurrentEnergyValue, 0, Math.Max(0, cap));
        return true;
    }

    internal static bool IsStarsLocked(PlayerCombatState pcs) {
        var player = PlayerRef(pcs);
        if (!StatLock.Active || !StatLock.IsLocal(player)) return false;
        return StatLock.Mods is { LockStars: true };
    }

    internal static bool TryGetLockedStars(PlayerCombatState pcs, out int locked) {
        locked = 0;
        if (!IsStarsLocked(pcs)) return false;
        locked = Math.Max(0, StatLock.Mods!.LockedStarsValue);
        return true;
    }
}
