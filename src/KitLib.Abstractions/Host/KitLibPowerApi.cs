namespace KitLib.Abstractions.Host;

public enum KitLibPowerTarget {
    Self,
    AllEnemies,
    Allies,
}

public sealed record KitLibAddPowerRequest(
    string PowerId,
    int Amount = 1,
    KitLibPowerTarget Target = KitLibPowerTarget.Self,
    ulong? TargetPlayerNetId = null);

public sealed record KitLibRemovePowerRequest(string PowerId, ulong? TargetPlayerNetId = null);

public sealed record KitLibClearPowersRequest(ulong? TargetPlayerNetId = null);

/// <summary>
/// Combat power mutations wired by KitLib.Cheat (ships with the KitLib product). String ids only.
/// </summary>
public static class KitLibPowerApi {
    public static Func<bool>? IsAvailable { get; set; }

    public static Func<KitLibAddPowerRequest, Task<KitLibRunItemResult>>? TryAddPower { get; set; }
    public static Func<KitLibRemovePowerRequest, Task<KitLibRunItemResult>>? TryRemovePower { get; set; }
    public static Func<KitLibClearPowersRequest, Task<KitLibRunItemResult>>? TryClearPowers { get; set; }
}
