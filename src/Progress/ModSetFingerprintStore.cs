using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KitLib.Modding;

namespace KitLib.Progress;

internal sealed class ModFingerprintEntry {
    public string Id { get; set; } = "";
    public string Version { get; set; } = "";
    public List<string> Dependencies { get; set; } = [];
}

internal sealed class ModSetFingerprintData {
    public string Hash { get; set; } = "";
    public DateTimeOffset UtcTimestamp { get; set; }
    public List<ModFingerprintEntry> Mods { get; set; } = [];
}

internal static class ModSetFingerprintStore {
    private static readonly JsonSerializerOptions JsonOpts = new() {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static string FilePath => DataPaths.FingerprintFile;

    public static ModSetFingerprintData? Load() {
        try {
            if (!File.Exists(FilePath))
                return null;

            return JsonSerializer.Deserialize<ModSetFingerprintData>(File.ReadAllText(FilePath), JsonOpts);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[ModChangeGuard] Failed to load mod fingerprint: {ex.Message}");
            return null;
        }
    }

    public static void Save(IReadOnlyList<KitLibModInfo> mods, string hash) {
        var data = new ModSetFingerprintData {
            Hash = hash,
            UtcTimestamp = DateTimeOffset.UtcNow,
            Mods = mods.Select(ToEntry).ToList(),
        };

        for (int attempt = 0; attempt < 3; attempt++) {
            try {
                Directory.CreateDirectory(DataPaths.BaseDir);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(data, JsonOpts));
                File.Move(tmp, FilePath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 2) {
                System.Threading.Thread.Sleep(40);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[ModChangeGuard] Failed to save mod fingerprint: {ex.Message}");
                return;
            }
        }
    }

    public static string ComputeHash(IReadOnlyList<KitLibModInfo> mods) {
        var lines = mods
            .OrderBy(m => m.Id, StringComparer.Ordinal)
            .Select(m => {
                var deps = m.Dependencies.Count == 0
                    ? ""
                    : string.Join(",", m.Dependencies.OrderBy(d => d, StringComparer.Ordinal));
                return $"{m.Id}|{m.Version}|{deps}";
            });

        var payload = string.Join("\n", lines);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    private static ModFingerprintEntry ToEntry(KitLibModInfo mod) =>
        new() {
            Id = mod.Id,
            Version = mod.Version,
            Dependencies = mod.Dependencies.Count == 0 ? [] : mod.Dependencies.ToList(),
        };
}
