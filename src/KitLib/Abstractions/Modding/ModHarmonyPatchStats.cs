namespace KitLib.Abstractions.Modding;

/// <summary>Harmony patch footprint attributed to one mod row in ModPanel.</summary>
public readonly record struct ModHarmonyPatchStats(
    int PatchOperations,
    int PatchedMethods,
    int HarmonyOwnerCount,
    IReadOnlyList<string> OwnerIds);
