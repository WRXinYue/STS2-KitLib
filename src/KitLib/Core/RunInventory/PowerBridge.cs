using KitLib.Abstractions.Host;
using KitLib.Host;

namespace KitLib.RunInventory;

/// <summary>Public power API; execution delegates to KitLib.Cheat when loaded.</summary>
public static class PowerBridge {
    public static bool IsAvailable =>
        KitLibHost.IsModuleLoaded(ModuleIds.Cheat)
        && KitLibPowerApi.IsAvailable?.Invoke() == true;

    public static Task<KitLibRunItemResult> TryAddPower(KitLibAddPowerRequest request) {
        if (!IsAvailable || KitLibPowerApi.TryAddPower == null)
            return Task.FromResult(KitLibRunItemResult.Fail("KitLib.Cheat power API is unavailable."));
        return KitLibPowerApi.TryAddPower(request);
    }

    public static Task<KitLibRunItemResult> TryRemovePower(string powerId, ulong? targetPlayerNetId = null) =>
        TryRemovePower(new KitLibRemovePowerRequest(powerId, targetPlayerNetId));

    public static Task<KitLibRunItemResult> TryRemovePower(KitLibRemovePowerRequest request) {
        if (!IsAvailable || KitLibPowerApi.TryRemovePower == null)
            return Task.FromResult(KitLibRunItemResult.Fail("KitLib.Cheat power API is unavailable."));
        return KitLibPowerApi.TryRemovePower(request);
    }

    public static Task<KitLibRunItemResult> TryClearPowers(ulong? targetPlayerNetId = null) =>
        TryClearPowers(new KitLibClearPowersRequest(targetPlayerNetId));

    public static Task<KitLibRunItemResult> TryClearPowers(KitLibClearPowersRequest request) {
        if (!IsAvailable || KitLibPowerApi.TryClearPowers == null)
            return Task.FromResult(KitLibRunItemResult.Fail("KitLib.Cheat power API is unavailable."));
        return KitLibPowerApi.TryClearPowers(request);
    }
}
