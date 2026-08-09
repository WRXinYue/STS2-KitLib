using KitLib.Abstractions.Host;
using KitLib.Host;

namespace KitLib.RunInventory;

/// <summary>Public runtime cheat / stat API; execution delegates to KitLib.Cheat when loaded.</summary>
public static class RuntimeCheatBridge {
    public static bool IsAvailable =>
        KitLibHost.IsModuleLoaded(ModuleIds.Cheat)
        && KitLibRuntimeCheatApi.IsAvailable?.Invoke() == true;

    public static KitLibCheatOpResult TrySetCheat(KitLibSetCheatRequest request) {
        if (!IsAvailable || KitLibRuntimeCheatApi.TrySetCheat == null)
            return KitLibCheatOpResult.Fail("KitLib.Cheat runtime cheat API is unavailable.");
        return KitLibRuntimeCheatApi.TrySetCheat(request);
    }

    public static KitLibStatOpResult TrySetStat(KitLibSetStatRequest request) {
        if (!IsAvailable || KitLibRuntimeCheatApi.TrySetStat == null)
            return KitLibStatOpResult.Fail("KitLib.Cheat runtime cheat API is unavailable.");
        return KitLibRuntimeCheatApi.TrySetStat(request);
    }
}
