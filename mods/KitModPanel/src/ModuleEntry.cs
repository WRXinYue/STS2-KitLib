using Godot;
using KitLib;
using KitLib.Abstractions.Host;
using KitLib.Host;
using KitLib.Hotkeys;
using KitLib.Integration;
using KitLib.UI;

namespace KitLib.ModPanelMod;

public static class ModuleEntry {
    public static void Initialize() {
        if (KitLibHost.IsModuleLoaded(KitLibModuleIds.ModPanel)) return;
        KitLibHost.AnnounceModule(KitLibModuleIds.ModPanel);
        KitLibHost.RegisterModSettingsPanelHost(new ModSettingsPanelHost());
        KitLibModSettingsUiBuilders.WireApi();
        WirePanelOps();
        KitLibNativeModSettingsBootstrap.RegisterKitLibPages();
        KitLibHost.NotifyPerfHudEnabledChanged = KitLibModSettingsUiBuilders.RefreshBoolToggles;
        KitLibHarmony.Apply(typeof(ModuleEntry).Assembly, KitLibModuleIds.ModPanel);
        KitLib.MainFile.Logger.Info("KitModPanel product initialized.");
    }

    static void WirePanelOps() {
        KitLibModPanelOps.TryCaptureHotkeySettingsInput = (keyObj, viewportObj) =>
            keyObj is InputEventKey key
            && viewportObj is Viewport viewport
            && KitLibHotkeySettingsSection.TryCaptureInputEvent(key, viewport);

        KitLibModPanelOps.TryHandleOpenModPanelHotkey = (keyObj, viewportObj) =>
            keyObj is InputEventKey key
            && viewportObj is Viewport viewport
            && ModPanelHotkeys.TryHandle(key, viewport);

        KitLibModPanelOps.CancelHotkeySettingsCapture = KitLibHotkeySettingsUi.CancelCapture;
        KitLibModPanelOps.BuildHotkeySettingsSection = compact =>
            KitLibHotkeySettingsUi.BuildSection(compact);
    }
}
