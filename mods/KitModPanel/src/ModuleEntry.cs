using KitLib.Abstractions.Host;
using KitLib.Abstractions.Modding;
using KitLib.Host;
using KitLib.Integration;

namespace KitLib.ModPanelMod;

public static class ModuleEntry {
    public static void Initialize() {
        if (KitLibHost.IsModuleLoaded(KitLibModuleIds.ModPanel)) return;
        KitLibHost.AnnounceModule(KitLibModuleIds.ModPanel);
        KitLibHost.RegisterModSettingsPanelHost(new ModSettingsPanelHost());
        KitLibNativeModSettingsBootstrap.RegisterKitLibPages();
        KitLibHost.NotifyPerfHudEnabledChanged = KitLibNativeModSettingsUi.RefreshBoolToggles;
        KitLibHarmony.Apply(typeof(ModuleEntry).Assembly, KitLibModuleIds.ModPanel);
        MainFile.Logger.Info("KitLib.ModPanel module initialized.");
    }
}
