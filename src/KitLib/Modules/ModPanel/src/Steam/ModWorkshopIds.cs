using System;
using System.IO;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.Steam;

/// <summary>
/// Workshop item id of a loaded mod. Newer game builds expose <c>Mod.workshopId</c>; stable
/// builds only have the install path, which still carries the id under <c>workshop/content</c>.
/// </summary>
internal static class ModWorkshopIds {
    internal static ulong? Resolve(Mod? mod) {
        if (mod == null)
            return null;

#if !STS2_STABLE_PROFILE
        if (mod.workshopId != 0)
            return mod.workshopId;
#endif

        return FromPath(mod.path);
    }

    private static ulong? FromPath(string? path) {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        const string marker = "/workshop/content/";
        var normalized = path.Replace('\\', '/');
        var idx = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;

        var parts = normalized[(idx + marker.Length)..].Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;

        return ulong.TryParse(parts[1], out var id) && id != 0 ? id : null;
    }
}
