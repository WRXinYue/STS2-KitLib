using KitLib.Abstractions.Modding;
using KitLib.Integration;

namespace KitLib.ModPanelMod;

sealed class ModSettingsPanelHost : IModSettingsPanelHost {
    public bool IsModuleLoaded => true;
    public bool IsRitsuBridgeAvailable => RitsuModSettingsBridge.IsAvailable;
}
