using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using KitLib.Modding;
using MegaCrit.Sts2.Core.Debug;

namespace KitLib.Feedback;

/// <summary>
/// Unpacks a KitLib / official-style feedback ZIP for DevMode load.
/// Does not write the player's live <c>current_run.save</c> slot.
/// </summary>
internal static class FeedbackImport {
    const long MaxEntryBytes = 80L * 1024 * 1024;
    const int MaxEntries = 256;

    internal readonly record struct Result(
        string ZipPath,
        string RunSavePath,
        string? ReplayMcrPath,
        bool HasCheckpoint,
        string CompatibilityNote);

    internal static Result? LastImport { get; private set; }

    internal static string ExtractDir => Path.Combine(DataPaths.BaseDir, "feedback-import");

    internal static bool TryImport(string zipPath, out Result result, out string error) {
        result = default;
        error = "";
        LastImport = null;

        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath)) {
            error = I18N.T("devmenu.loadFeedback.err.notFound", "ZIP not found.");
            return false;
        }

        try {
            if (Directory.Exists(ExtractDir))
                Directory.Delete(ExtractDir, recursive: true);
            Directory.CreateDirectory(ExtractDir);

            var extractRoot = Path.GetFullPath(ExtractDir);
            using var archive = ZipFile.OpenRead(zipPath);
            int n = 0;
            foreach (var entry in archive.Entries) {
                if (string.IsNullOrEmpty(entry.Name))
                    continue;
                if (++n > MaxEntries) {
                    error = I18N.T("devmenu.loadFeedback.err.tooMany", "ZIP has too many files.");
                    return false;
                }
                if (entry.Length > MaxEntryBytes) {
                    error = I18N.T("devmenu.loadFeedback.err.tooLarge", "ZIP entry too large: {0}", entry.FullName);
                    return false;
                }

                var dest = SafeDestPath(extractRoot, entry.FullName);
                if (dest == null)
                    continue;

                var dir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                entry.ExtractToFile(dest, overwrite: true);
            }

            var runSave = FindFile(extractRoot, "current_run.save")
                ?? FindFile(extractRoot, "current_run_mp.save");
            if (runSave == null) {
                error = I18N.T("devmenu.loadFeedback.err.noRunSave", "No current_run.save in this ZIP.");
                return false;
            }

            result = new Result(
                zipPath,
                runSave,
                FindFile(extractRoot, "latest.mcr"),
                Directory.Exists(Path.Combine(extractRoot, "combat-checkpoint")),
                FormatCompatibilityNote(extractRoot));
            LastImport = result;
            return true;
        }
        catch (Exception ex) {
            error = ex.Message;
            return false;
        }
    }

    const int MaxListedMods = 8;

    static string FormatCompatibilityNote(string extractRoot) {
        var lines = new List<string>();
        TryAddModNotes(extractRoot, lines);
        TryAddGameVersionNote(extractRoot, lines);
        return lines.Count == 0 ? "" : "\n\n" + string.Join("\n", lines);
    }

    static void TryAddModNotes(string extractRoot, List<string> lines) {
        var zipMods = ReadZipMods(extractRoot);
        if (zipMods.Count == 0)
            return;

        var current = new Dictionary<string, (string Name, string Version)>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in ModRuntime.Catalog.GetSnapshot()) {
            if (string.IsNullOrWhiteSpace(m.Id))
                continue;
            current[m.Id] = (string.IsNullOrWhiteSpace(m.DisplayName) ? m.Id : m.DisplayName, m.Version ?? "");
        }

        var missing = new List<string>();
        var mismatched = new List<string>();
        foreach (var zip in zipMods) {
            if (!current.TryGetValue(zip.Id, out var now)) {
                missing.Add(FormatModLabel(zip.Name, zip.Version));
                continue;
            }
            if (SameVersion(zip.Version, now.Version))
                continue;
            mismatched.Add(I18N.T("devmenu.loadFeedback.compat.item", "{0} {1} → {2}",
                zip.Name, DisplayVersion(zip.Version), DisplayVersion(now.Version)));
        }

        if (missing.Count > 0)
            lines.Add(I18N.T("devmenu.loadFeedback.compat.missing", "Missing mods: {0}", JoinCapped(missing)));
        if (mismatched.Count > 0)
            lines.Add(I18N.T("devmenu.loadFeedback.compat.mismatch", "Version mismatch: {0}", JoinCapped(mismatched)));
    }

    static void TryAddGameVersionNote(string extractRoot, List<string> lines) {
        var zipVersion = ReadReleaseVersion(extractRoot);
        if (string.IsNullOrWhiteSpace(zipVersion))
            return;
        var now = ReleaseInfoManager.Instance.ReleaseInfo?.Version ?? "";
        if (string.IsNullOrWhiteSpace(now) || SameVersion(zipVersion, now))
            return;
        lines.Add(I18N.T("devmenu.loadFeedback.compat.game", "Game version: ZIP {0} / now {1}",
            zipVersion.Trim(), now.Trim()));
    }

    static List<(string Id, string Name, string Version)> ReadZipMods(string extractRoot) {
        var list = new List<(string Id, string Name, string Version)>();
        var path = FindFile(extractRoot, "report-meta.json");
        if (path == null)
            return list;
        try {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("mods", out var mods) || mods.ValueKind != JsonValueKind.Array)
                return list;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var el in mods.EnumerateArray()) {
                var id = JsonString(el, "id");
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                    continue;
                var name = JsonString(el, "name");
                if (string.IsNullOrWhiteSpace(name))
                    name = id;
                list.Add((id, name, JsonString(el, "version")));
            }
        }
        catch (Exception) {
        }
        return list;
    }

    static string? ReadReleaseVersion(string extractRoot) {
        var path = FindFile(extractRoot, "release_info.json");
        if (path == null)
            return null;
        try {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        }
        catch (Exception) {
        }
        return null;
    }

    static string JsonString(JsonElement el, string name) {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var p))
            return "";
        return p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
    }

    static string JoinCapped(List<string> items) {
        if (items.Count <= MaxListedMods)
            return string.Join(", ", items);
        var shown = string.Join(", ", items.Take(MaxListedMods));
        return shown + ", " + I18N.T("devmenu.loadFeedback.compat.more", "+{0} more", items.Count - MaxListedMods);
    }

    static string FormatModLabel(string name, string version) {
        var ver = DisplayVersion(version);
        return ver == "?" ? name : $"{name} {ver}";
    }

    static string DisplayVersion(string version) {
        var t = version.Trim();
        return t.Length == 0 ? "?" : t;
    }

    static bool SameVersion(string a, string b) {
        return string.Equals(NormalizeVersion(a), NormalizeVersion(b), StringComparison.OrdinalIgnoreCase);
    }

    static string NormalizeVersion(string version) {
        var t = version.Trim();
        if (t.Length >= 2 && (t[0] == 'v' || t[0] == 'V') && char.IsDigit(t[1]))
            t = t[1..];
        return t;
    }

    static string? FindFile(string root, string fileName) =>
        Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories)
            .OrderBy(p => p.Length)
            .FirstOrDefault();

    static string? SafeDestPath(string extractRoot, string entryName) {
        var combined = Path.GetFullPath(Path.Combine(
            extractRoot,
            entryName.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = extractRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(combined, extractRoot, StringComparison.OrdinalIgnoreCase))
            return null;
        return combined;
    }
}
