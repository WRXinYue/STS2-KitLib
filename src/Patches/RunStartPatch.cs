using System;
using KitLib.Cheat;
using KitLib.Presets;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib.Patches;

[HarmonyPatch(typeof(RunManager))]
public static class RunStartPatch {
    /// <summary>
    /// Disable save persistence for dev-mode runs.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(nameof(RunManager.SetUpNewSinglePlayer))]
    public static void DisableSaveForDevRun(ref bool shouldSave) {
        if (KitLibState.InDevRun) {
            shouldSave = false;
            MainFile.Logger.Info("KitLib: Save disabled for dev run.");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(RunManager.Launch))]
    public static void InjectDevContent(RunState __result) {
        if (!KitLibState.InDevRun)
            return;

        MainFile.Logger.Info("KitLib: Injecting dev mode content into run...");

        foreach (var player in __result.Players) {
            InjectForPlayer(player);
        }

        ApplyPendingRestart(__result);
        MainFile.Logger.Info("KitLib: Dev mode content injected successfully.");
    }

    private static void ApplyPendingRestart(RunState runState) {
        // Apply carried-over gold (direct, synchronous)
        if (KitLibState.PendingRestartGold.HasValue) {
            var gold = KitLibState.PendingRestartGold.Value;
            foreach (var player in runState.Players)
                player.Gold = gold;
            MainFile.Logger.Info($"[KitLib] Restart: applied gold {gold}.");
        }

        // Apply carried-over cards / relics (async via game command queue)
        if (CheatRestartState.PendingRestartPreset != null) {
            var preset = CheatRestartState.PendingRestartPreset;
            var scope = CheatRestartState.PendingRestartScope;
            MainFile.Logger.Info($"[KitLib] Restart: scheduling preset apply (scope: {scope}).");
            TaskHelper.RunSafely(PresetManager.ApplyToRunAsync(preset, scope));
        }

        CheatRestartState.ClearPresetRestart();
        KitLibState.ClearPendingRestart();
    }

    public static void OnRunEnded() {
        KitLibState.OnRunEnded();
    }

    private static void InjectForPlayer(Player player) {
        if (KitLibState.MaxEnergy > 0) {
            player.MaxEnergy = KitLibState.MaxEnergy;
            MainFile.Logger.Info($"KitLib: Set max energy to {KitLibState.MaxEnergy}");
        }
    }
}

[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SaveProgressFile))]
public static class SaveProgressPatch {
    public static bool Prefix() {
        if (KitLibState.InDevRun) {
            MainFile.Logger.Info("KitLib: Skipping progress save for dev run.");
            return false;
        }
        return true;
    }
}

/// <summary>
/// Intercepts NGame.StartNewSingleplayerRun to inject PendingRestartSeed.
/// NGame.DebugSeedOverride cannot be used here because NCharacterSelectScreen.BeginRun
/// overwrites it from its own settings (and clears it) before the run launches.
/// </summary>
[HarmonyPatch(typeof(NGame), nameof(NGame.StartNewSingleplayerRun))]
public static class SeedInjectPatch {
    public static void Prefix(ref string seed) {
        if (KitLibState.PendingRestartSeed == null) return;

        var canonicalized = SeedHelper.CanonicalizeSeed(KitLibState.PendingRestartSeed);
        MainFile.Logger.Info($"[KitLib] SeedInject: overriding seed '{seed}' → '{canonicalized}'.");
        seed = canonicalized;

        // Consumed — clear so a subsequent normal run is not affected.
        KitLibState.PendingRestartSeed = null;
    }
}
