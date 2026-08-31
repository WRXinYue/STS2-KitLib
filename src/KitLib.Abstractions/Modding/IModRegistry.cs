namespace KitLib.Abstractions.Modding;

/// <summary>All mods the game scanned (<c>ModManager.Mods</c>), including disabled/failed.</summary>
public interface IModRegistry {
    IReadOnlyList<KitLibModEntry> GetAllEntries();

    KitLibModEntry? TryGet(string id, ModEntrySource source);

    /// <summary>
    /// Install folder for <paramref name="modId"/> from the official mod list
    /// (<c>Mod.manifest.id</c> + <c>Mod.path</c>). Prefers a loaded copy.
    /// Workshop items use Steam's numeric folder, not <c>mods/&lt;id&gt;</c>.
    /// </summary>
    string? TryGetInstallDirectory(string modId);
}
