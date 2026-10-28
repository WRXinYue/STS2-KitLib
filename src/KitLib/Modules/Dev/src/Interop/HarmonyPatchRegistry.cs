using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitLib.Interop;

/// <summary>
/// Single source of truth for Harmony owner → human-readable metadata (docs + in-game analysis).
/// Loads from the embedded resource <c>KitLib.Data.harmony-patch-registry.json</c> in the DLL,
/// with optional override via an external <c>harmony-patch-registry.json</c> placed next to the DLL.
/// </summary>
public sealed class HarmonyPatchRegistry {
    public const string FileName = "harmony-patch-registry.json";
    private const string EmbeddedResourceName = "KitLib.Data.harmony-patch-registry.json";

    private readonly Dictionary<string, PatchDocEntry> _byOwner;

    private HarmonyPatchRegistry(Dictionary<string, PatchDocEntry> byOwner) {
        _byOwner = byOwner;
    }

    public static HarmonyPatchRegistry Empty { get; } = new(new Dictionary<string, PatchDocEntry>(StringComparer.OrdinalIgnoreCase));

    public int Count => _byOwner.Count;

    public bool TryGet(string owner, out PatchDocEntry entry) {
        if (_byOwner.TryGetValue(owner ?? "", out var e)) {
            entry = e;
            return true;
        }

        entry = null!;
        return false;
    }

    private static HarmonyPatchRegistry? _cached;
    private static string? _cachedError;
    private static bool _cacheValid;

    /// <summary>Discard the cached registry so the next <see cref="Load"/> call re-reads the file.</summary>
    public static void InvalidateCache() => _cacheValid = false;

    /// <summary>
    /// Loads registry; on failure returns <see cref="Empty"/> and sets <paramref name="error"/>.
    /// Result is cached until <see cref="InvalidateCache"/> is called.
    /// </summary>
    public static HarmonyPatchRegistry Load(out string? error) {
        if (_cacheValid) {
            error = _cachedError;
            return _cached!;
        }

        var result = LoadUncached(out error);
        _cached = result;
        _cachedError = error;
        _cacheValid = true;
        return result;
    }

    private static HarmonyPatchRegistry LoadUncached(out string? error) {
        error = null;
        var modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        var externalPath = Path.Combine(modDir, FileName);

        if (File.Exists(externalPath)) {
            string json;
            try {
                json = File.ReadAllText(externalPath);
            }
            catch (Exception ex) {
                var fb = TryLoadEmbedded(out _);
                if (fb != null) {
                    error = $"{ex.Message}  ({FileName} unreadable — using embedded registry.)";
                    return fb;
                }

                error = ex.Message;
                return Empty;
            }

            if (TryParse(json, out var fromExternal))
                return fromExternal;

            var fb2 = TryLoadEmbedded(out _);
            if (fb2 != null) {
                error = $"Invalid {FileName}; using embedded registry.";
                return fb2;
            }

            error = $"Invalid {FileName} and embedded registry unavailable.";
            return Empty;
        }

        return TryLoadEmbedded(out error) ?? Empty;
    }

    private static HarmonyPatchRegistry? TryLoadEmbedded(out string? error) {
        error = null;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedResourceName);
        if (stream == null) {
            error = $"Missing embedded resource '{EmbeddedResourceName}'.";
            return null;
        }

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        if (TryParse(json, out var reg))
            return reg;

        error = "Embedded registry JSON failed to parse.";
        return null;
    }

    private static bool TryParse(string json, out HarmonyPatchRegistry registry) {
        registry = Empty;
        try {
            var file = JsonSerializer.Deserialize<RegistryFileDto>(json, JsonOptions);
            if (file?.Entries == null || file.Entries.Count == 0)
                return false;

            var dict = new Dictionary<string, PatchDocEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in file.Entries) {
                var o = row.Owner?.Trim();
                if (string.IsNullOrEmpty(o)) continue;
                dict[o] = new PatchDocEntry(
                    o,
                    NullIfEmpty(row.Category),
                    NullIfEmpty(row.DisplayName),
                    NullIfEmpty(row.Summary),
                    NullIfEmpty(row.DocUrl));
            }

            if (dict.Count == 0)
                return false;

            registry = new HarmonyPatchRegistry(dict);
            return true;
        }
        catch {
            return false;
        }
    }

    private static string? NullIfEmpty(string? s) {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return s.Trim();
    }

    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed class RegistryFileDto {
        public int SchemaVersion { get; set; }
        public List<EntryDto>? Entries { get; set; }
    }

    private sealed class EntryDto {
        public string? Owner { get; set; }
        public string? Category { get; set; }
        public string? DisplayName { get; set; }
        public string? Summary { get; set; }
        public string? DocUrl { get; set; }
    }
}

/// <summary>One row in <see cref="HarmonyPatchRegistry"/>.</summary>
public sealed record PatchDocEntry(
    string Owner,
    string? Category,
    string? DisplayName,
    string? Summary,
    string? DocUrl);
