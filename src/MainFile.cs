using DevMode.AI;
using DevMode.CombatStats;
using DevMode.EnemyIntent;
using DevMode.Feedback;
using DevMode.Interop;
using DevMode.Multiplayer.Cheat;
using DevMode.Patches;
using DevMode.Scripts;
using DevMode.Settings;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace DevMode;

[ModInitializer(nameof(Initialize))]
public class MainFile {
    public const string ModID = "DevMode";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModID, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize() {
        Logger.Info("DevMode initializing...");

        DevModeInstanceRegistry.Register();

        // Load persisted settings (theme, etc.) before anything else
        SettingsStore.Load();

        // Initialize localization before anything else
        I18N.Initialize();

        // Start capturing log entries for the in-game log viewer
        InstanceLogWriter.Initialize();
        LogCollector.Initialize();
        CrashRecoveryHooks.Register();
        CrashRecoveryStore.MarkSessionStarted();

        if (DevModeInstanceRegistry.IsDualInstanceActive())
            Logger.Info($"[DevMode] Dual-instance mode ({DevModeInstanceRegistry.ActiveInstanceCount()} processes).");

        ScriptManager.Initialize();
        ScriptBridge.Start();

        FrameworkBridge.Initialize();
        AiPlayInitializer.Initialize();
        MpCheatSync.Initialize();

        CombatStatsTracker.Initialize();
        MonsterIntentOverlayTracker.Initialize();
        MonsterIntentOverrides.Initialize();

        var harmony = new Harmony(ModID);
        harmony.PatchAll();
        ScriptCardPlayedPatch.TryApply(harmony);
        Logger.Info("DevMode initialized — Harmony patches applied.");
    }
}
