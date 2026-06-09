namespace KitLib.Abstractions.Modding;

/// <summary>Read-only view of mods the game has already scanned and loaded.</summary>
public interface IModCatalog {
    /// <summary>Copies current loaded-mod entries that have a non-empty manifest <c>id</c>.</summary>
    IReadOnlyList<KitLibModInfo> GetSnapshot();

    /// <summary>Fast membership checks (e.g. log line attribution). Empty if no mods loaded.</summary>
    HashSet<string> GetIdSet(StringComparer? comparer = null);
}
