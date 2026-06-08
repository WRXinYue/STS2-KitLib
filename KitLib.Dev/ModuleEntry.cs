using HarmonyLib;
using KitLib.Abstractions.Host;
using KitLib.CombatStats;
using KitLib.EnemyIntent;
using KitLib.Host;
using KitLib.Interop;
using KitLib.Mcp;
using KitLib.Scripts;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.Dev;

[ModInitializer(nameof(Initialize))]
public static class ModuleEntry {
    public static void Initialize() {
        KitLibHost.AnnounceModule(KitLibModuleIds.Dev);
        KitLibHost.IsDualInstanceActive = KitLibInstanceRegistry.IsDualInstanceActive;
        KitLibInstanceRegistry.Register();
        ScriptManager.Initialize();
        ScriptBridge.Start();
        McpBridge.Start();
        FrameworkBridge.Initialize();
        CombatStatsTracker.Initialize();
        MonsterIntentOverlayTracker.Initialize();
        MonsterIntentOverrides.Initialize();
        DevTabRegistration.Register();

        KitLibFeaturesPatches.EnsureApplied();
        MainFile.Logger.Info("KitLib.Dev module initialized.");
    }
}
