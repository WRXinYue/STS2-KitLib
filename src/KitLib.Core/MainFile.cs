using KitLib.Diagnostics;
using KitLib.Host;
using KitLib.Multiplayer.Play.Patches;
using KitLib.Patches;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib;

[ModInitializer(nameof(Initialize))]
public class MainFile {
    public const string ModID = "KitLib";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModID, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize() {
        Logger.Info("KitLib Core initializing...");
        KitLibStartupAudit.Measure("dependencies", ModDependencyLoader.EnsureLoaded);
        KitLibStartupAudit.Measure("runtimeProfile", Sts2RuntimeProfile.Initialize);
        KitLibStartupAudit.Measure("dataPaths", DataPaths.EnsurePinnedOnMainThread);
        LegacyInstancesDirCleanup.ScheduleOnStartup();
        KitLibStartupAudit.Measure("settings", SettingsStore.Load);
        // Scoped apply: User/Cheat patches live in this assembly after the host merge.
        KitLibStartupAudit.Measure("coreHarmony", () => KitLibHarmony.ApplyOnly(
            typeof(MainFile).Assembly,
            ModID,
            typeof(MultiplayerModSyncPatch),
            typeof(JoinFlowCompatPatch),
            typeof(HostEnqueuePatch),
            typeof(CombatActionFlightPatch)));
        KitLibStartupAudit.Measure("hostBootstrap", KitLibHost.Bootstrap);
        KitLibStartupAudit.Measure("i18n", I18N.Initialize);
        Logger.Info("KitLib Core initialized.");
        KitLibStartupAudit.LogCoreOnlyReportIfNeeded();
    }
}
