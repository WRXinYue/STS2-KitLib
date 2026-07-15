using KitLib.Diagnostics;
using KitLib.Host;
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
        KitLibStartupAudit.Measure("logBridge", ModKitLibLogBridge.Initialize);
        KitLibStartupAudit.Measure("dataPaths", DataPaths.EnsurePinnedOnMainThread);
        LegacyInstancesDirCleanup.ScheduleOnStartup();
        KitLibStartupAudit.Measure("settings", SettingsStore.Load);
        KitLibStartupAudit.Measure("coreHarmony", () => KitLibHarmony.Apply(typeof(MainFile).Assembly, ModID));
        KitLibStartupAudit.Measure("hostBootstrap", KitLibHost.Bootstrap);
        KitLibStartupAudit.Measure("i18n", I18N.Initialize);
        Logger.Info("KitLib Core initialized.");
        KitLibStartupAudit.LogCoreOnlyReportIfNeeded();
    }
}
