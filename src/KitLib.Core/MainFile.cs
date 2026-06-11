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
        ModDependencyLoader.EnsureLoaded();
        Sts2RuntimeProfile.Initialize();
        ModKitLibLogBridge.Initialize();
        DataPaths.EnsurePinnedOnMainThread();
        SettingsStore.Load();
        KitLibHarmony.Apply(typeof(MainFile).Assembly, ModID);
        KitLibHost.Bootstrap();
        I18N.Initialize();
        Logger.Info("KitLib Core initialized.");
    }
}
