using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using KitLib.Cheat;
using KitLib.Presets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib.Patches;

[HarmonyPatch(typeof(RunManager))]
public static class RunStartPatch {
    /// <summary>
    /// Disable save persistence for dev-mode runs.
    /// </summary>
    [HarmonyTargetMethod]
    static MethodBase? DisableSaveTarget() =>
        AccessTools.Method(typeof(RunManager), "SetUpNewSingleplayer")
        ?? AccessTools.Method(typeof(RunManager), "SetUpNewSinglePlayer");

    [HarmonyPrefix]
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
            KitLog.Info($"Restart: applied gold {gold}.");
        }

        // Apply carried-over cards / relics (async via game command queue)
        if (CheatRestartState.PendingRestartPreset != null) {
            var preset = CheatRestartState.PendingRestartPreset;
            var scope = CheatRestartState.PendingRestartScope;
            KitLog.Info($"Restart: scheduling preset apply (scope: {scope}).");
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

/// <summary>Same rail slide, started one step earlier at the embark click so it is visible.</summary>
[HarmonyPatch(typeof(NCharacterSelectScreen), "OnEmbarkPressed")]
internal static class EmbarkWrapPatch {
    [HarmonyPrefix]
    static bool Prefix(NCharacterSelectScreen __instance, object[] __args) =>
        !EntrySlideGate.Defer(__instance, EntrySlideGate.Embark, __instance, __args);
}

/// <summary>
/// <para xml:lang="en">Holds the click that enters a run for one wrap animation. The game builds a run inside that single frame, so without the hold there is no frame left in which the rail could slide out.</para>
/// <para xml:lang="zh-CN">把进入游戏的那次点击推迟一个收起动画的时长；游戏在同一帧里就建完全局，不推迟根本没有帧用来播滑出。</para>
/// </summary>
internal static class EntrySlideGate {
    internal static readonly MethodInfo ContinueAsync =
        AccessTools.Method(typeof(NMainMenu), "OnContinueButtonPressedAsync");
    internal static readonly MethodInfo Embark =
        AccessTools.Method(typeof(NCharacterSelectScreen), "OnEmbarkPressed");

    private static bool _pending;

    /// <summary>Defer the entry call; false means the caller should just run it now.</summary>
    internal static bool Defer(Node host, MethodInfo entry, object instance, object[] args) {
        if (entry == null || _pending)
            return false;
        // Nothing on screen to slide out; the call can proceed at once.
        if (!UI.MainMenuRailHost.IsRailOnScreen)
            return false;

        var tree = host.GetTree();
        if (tree == null)
            return false;

        UI.MainMenuRailHost.SlideOutForEntry();
        _pending = true;
        tree.CreateTimer(UI.KitLibScreenFrame.AnimationSeconds).Timeout += () => {
            _pending = false;
            try {
                entry.Invoke(instance, args);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"KitLib entry slide gate: {ex.Message}");
            }
        };
        return true;
    }
}

/// <summary>Continue from the main menu loads a run straight away, so it takes the same gate.</summary>
[HarmonyPatch(typeof(NMainMenu), "OnContinueButtonPressedAsync")]
internal static class ContinueEntryWrapPatch {
    [HarmonyPrefix]
    static bool Prefix(NMainMenu __instance) =>
        !EntrySlideGate.Defer(__instance, EntrySlideGate.ContinueAsync, __instance, Array.Empty<object>());
}

/// <summary>
/// Dev and pseudo-coop runs may include mod epochs that fail combat replay serialization on exit.
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.WriteReplay))]
internal static class DevCombatReplaySkipPatch {
    public static bool Prefix(RunManager __instance, bool stopRecording) {
        if (!KitLibState.InDevRun && !KitLibState.IsPseudoCoopSession)
            return true;

        if (stopRecording && __instance.CombatReplayWriter.IsRecordingReplay)
            __instance.CombatReplayWriter.StopRecording();

        return false;
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
        KitLog.Info($"SeedInject: overriding seed '{seed}' → '{canonicalized}'.");
        seed = canonicalized;

        // Consumed — clear so a subsequent normal run is not affected.
        KitLibState.PendingRestartSeed = null;
    }
}
