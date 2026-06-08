using KitLib.AI;
using KitLib.CombatStats;
using KitLib.EnemyIntent;
using KitLib.Feedback;
using KitLib.Interop;
using KitLib.Mcp;
using KitLib.Multiplayer.Cheat;
using KitLib.Patches;
using KitLib.Scripts;
using KitLib.Settings;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib;

[ModInitializer(nameof(Initialize))]
public class MainFile {
    public const string ModID = "KitLib";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModID, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize() {
        Logger.Info("KitLib initializing...");

        KitLibInstanceRegistry.Register();

        // Load persisted settings (theme, etc.) before anything else
        SettingsStore.Load();

        // Initialize localization before anything else
        I18N.Initialize();

        // Start capturing log entries for the in-game log viewer
        InstanceLogWriter.Initialize();
        LogCollector.Initialize();
        CrashRecoveryHooks.Register();
        CrashRecoveryStore.MarkSessionStarted();

        if (KitLibInstanceRegistry.IsDualInstanceActive())
            Logger.Info($"[KitLib] Dual-instance mode ({KitLibInstanceRegistry.ActiveInstanceCount()} processes).");

        ScriptManager.Initialize();
        ScriptBridge.Start();
        McpBridge.Start();

        FrameworkBridge.Initialize();
        AiPlayInitializer.Initialize();
        MpCheatSync.Initialize();

        CombatStatsTracker.Initialize();
        MonsterIntentOverlayTracker.Initialize();
        MonsterIntentOverrides.Initialize();

        var harmony = new Harmony(ModID);
        harmony.PatchAll();
        ScriptCardPlayedPatch.TryApply(harmony);
        Logger.Info("KitLib initialized — Harmony patches applied.");
    }
}
