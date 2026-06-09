using System;
using System.IO;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Modding;
using KitLib.Modding;
namespace KitLib.UI;
/// <summary>
/// Resolves loaded-mod manifest fields for the mod panel sidebar header (same idea as RitsuLib
/// <c>ModSettingsModInfoResolver</c>, scoped to vanilla <see cref="Mod" /> + <c>res://&lt;id&gt;/mod_image.png</c>).
/// </summary>
internal static class ModPanelModBanner {
    internal static Mod? TryFindMod(string modId) {
        if (string.IsNullOrWhiteSpace(modId))
            return null;
        foreach (var m in ModManagerLoadedMods.Enumerate()) {
            var id = m.manifest?.id;
            if (string.Equals(id, modId, StringComparison.OrdinalIgnoreCase))
                return m;
        }
        foreach (var m in ModManagerLoadedMods.Enumerate()) {
            if (string.IsNullOrWhiteSpace(m.path))
                continue;
            var trimmed = m.path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var folder = Path.GetFileName(trimmed);
            if (string.Equals(folder, modId, StringComparison.OrdinalIgnoreCase))
                return m;
        }
        return null;
    }
    internal static string ResolveTitle(Mod? mod, string modId) {
        if (mod?.manifest is ModManifest mm && !string.IsNullOrWhiteSpace(mm.name))
            return mm.name;
        if (mod != null) {
            var n = GetManifestMemberString(mod.manifest, "name", "Name");
            if (!string.IsNullOrWhiteSpace(n))
                return n;
        }
        return string.IsNullOrWhiteSpace(modId) ? "—" : modId;
    }
    internal static string? ResolveVersion(Mod? mod) {
        if (mod?.manifest is ModManifest mm && !string.IsNullOrWhiteSpace(mm.version))
            return mm.version;
        return mod == null ? null : GetManifestMemberString(mod.manifest, "version", "Version");
    }
    internal static string? ResolveAuthor(Mod? mod) {
        if (mod?.manifest is ModManifest mm && !string.IsNullOrWhiteSpace(mm.author))
            return mm.author;
        return mod == null ? null : GetManifestMemberString(mod.manifest, "author", "Author");
    }
    internal static string? ResolveDescription(Mod? mod, int maxLen = 220) {
        string? d;
        if (mod?.manifest is ModManifest mm && !string.IsNullOrWhiteSpace(mm.description))
            d = mm.description;
        else
            d = mod == null ? null : GetManifestMemberString(mod.manifest, "description", "Description");
        if (string.IsNullOrWhiteSpace(d))
            return null;
        d = d.Trim().Replace("\r\n", "\n");
        return d.Length <= maxLen ? d : d[..maxLen].TrimEnd() + "…";
    }
    internal static Texture2D? TryLoadModIcon(Mod? mod, string modId) {
        var id = mod?.manifest is ModManifest mm ? mm.id : null;
        foreach (var key in new[] { id, modId }) {
            if (string.IsNullOrWhiteSpace(key))
                continue;
            var tex = TryLoadVanillaModImageRes(key);
            if (tex != null)
                return tex;
        }
        return null;
    }
    internal static string FormatVersionBadgeText(string raw) {
        var t = raw.Trim();
        if (t.Length == 0)
            return string.Empty;
        if (t.StartsWith('v') || t.StartsWith('V'))
            t = t[1..].TrimStart();
        return $"V{t}".ToUpperInvariant();
    }
    private static Texture2D? TryLoadVanillaModImageRes(string manifestId) {
        var path = $"res://{manifestId}/mod_image.png";
        try {
            if (!ResourceLoader.Exists(path))
                return null;
            return PreloadManager.Cache.GetAsset<Texture2D>(path);
        }
        catch {
            try {
                return GD.Load<Texture2D>(path);
            }
            catch {
                return null;
            }
        }
    }
    private static string? GetManifestMemberString(object? manifest, params string[] names) {
        if (manifest == null)
            return null;
        var t = manifest.GetType();
        foreach (var name in names) {
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p?.GetValue(manifest) is string s && !string.IsNullOrWhiteSpace(s))
                return s;
            var f = t.GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (f?.GetValue(manifest) is string s2 && !string.IsNullOrWhiteSpace(s2))
                return s2;
        }
        return null;
    }
}