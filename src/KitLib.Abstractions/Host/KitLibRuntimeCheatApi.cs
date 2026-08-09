namespace KitLib.Abstractions.Host;

public enum KitLibRunStat {
    Gold,
    CurrentHp,
    MaxHp,
    CurrentEnergy,
    MaxEnergy,
    Stars,
    OrbSlots,
    PotionSlots,
}

public sealed record KitLibCheatOpResult(
    bool Ok,
    string? Error = null,
    string? Cheat = null,
    bool? Enabled = null,
    float? Value = null) {
    public static KitLibCheatOpResult Success(string cheat, bool? enabled = null, float? value = null) =>
        new(true, null, cheat, enabled, value);

    public static KitLibCheatOpResult Fail(string error) => new(false, error);
}

public sealed record KitLibStatOpResult(
    bool Ok,
    string? Error = null,
    string? Stat = null,
    int Value = 0,
    bool Locked = false) {
    public static KitLibStatOpResult Success(string stat, int value, bool locked) =>
        new(true, null, stat, value, locked);

    public static KitLibStatOpResult Fail(string error) => new(false, error);
}

public sealed record KitLibSetStatRequest(
    KitLibRunStat Stat,
    int Value,
    bool? LockEnabled = null);

public sealed record KitLibSetCheatRequest(
    string Cheat,
    bool? Enabled = null,
    float? Value = null);

/// <summary>
/// Runtime / patch cheat toggles and run stat edits, wired by KitLib.Cheat.
/// Cheat names match MCP <c>dev_set_cheat</c> ids (e.g. god_mode, freeze_enemies).
/// </summary>
public static class KitLibRuntimeCheatApi {
    public static Func<bool>? IsAvailable { get; set; }

    public static Func<KitLibSetCheatRequest, KitLibCheatOpResult>? TrySetCheat { get; set; }
    public static Func<KitLibSetStatRequest, KitLibStatOpResult>? TrySetStat { get; set; }
}
