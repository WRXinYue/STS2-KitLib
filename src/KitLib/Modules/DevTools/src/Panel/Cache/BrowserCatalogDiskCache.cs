using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using KitLib;
using KitLib.Progress;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.UI;

/// <summary>
/// Persists sorted power/relic id lists under mod_data so cold starts skip re-sorting display names.
/// </summary>
internal static class BrowserCatalogDiskCache {
    private const string CacheFileName = "browser-catalog-v1.json";

    private static readonly JsonSerializerOptions JsonOpts = new() {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed class CatalogFile {
        public string CacheKey { get; set; } = "";
        public List<string> PowerIds { get; set; } = [];
        public List<string> RelicIds { get; set; } = [];
    }

    private static string CachePath => Path.Combine(DataPaths.BaseDir, CacheFileName);

    internal static List<PowerModel>? TryLoadSortedPowers() {
        var file = Load();
        if (file?.PowerIds.Count > 0 != true)
            return null;
        return ResolveModels(file.PowerIds, ModelDb.AllPowers);
    }

    internal static List<RelicModel>? TryLoadSortedRelics() {
        var file = Load();
        if (file?.RelicIds.Count > 0 != true)
            return null;
        return ResolveModels(file.RelicIds, ModelDb.AllRelics);
    }

    internal static void SaveSortedPowers(IReadOnlyList<PowerModel> powers) {
        var file = LoadOrCreate();
        file.PowerIds = powers.Select(IdOf).Where(id => id.Length > 0).ToList();
        Save(file);
    }

    internal static void SaveSortedRelics(IReadOnlyList<RelicModel> relics) {
        var file = LoadOrCreate();
        file.RelicIds = relics.Select(IdOf).Where(id => id.Length > 0).ToList();
        Save(file);
    }

    private static CatalogFile LoadOrCreate() {
        var file = Load();
        if (file != null && file.CacheKey == ResolveCacheKey())
            return file;
        return new CatalogFile { CacheKey = ResolveCacheKey() };
    }

    private static CatalogFile? Load() {
        try {
            if (!File.Exists(CachePath))
                return null;

            var file = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(CachePath), JsonOpts);
            if (file == null || file.CacheKey != ResolveCacheKey())
                return null;
            return file;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"BrowserCatalogDiskCache: load failed: {ex.Message}");
            return null;
        }
    }

    private static void Save(CatalogFile file) {
        file.CacheKey = ResolveCacheKey();
        for (int attempt = 0; attempt < 3; attempt++) {
            try {
                Directory.CreateDirectory(DataPaths.BaseDir);
                var tmp = CachePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(file, JsonOpts));
                File.Move(tmp, CachePath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 2) {
                System.Threading.Thread.Sleep(40);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"BrowserCatalogDiskCache: save failed: {ex.Message}");
                return;
            }
        }
    }

    private static string ResolveCacheKey() {
        var modHash = ModSetFingerprintStore.Load()?.Hash;
        if (string.IsNullOrEmpty(modHash))
            modHash = "nomods";
        return $"{modHash}|{I18N.LangCode}";
    }

    private static List<TModel> ResolveModels<TModel>(IReadOnlyList<string> ids, IEnumerable<TModel> source)
        where TModel : class {
        var byId = source
            .Select(model => (Id: IdOf(model), Model: model))
            .Where(entry => entry.Id.Length > 0)
            .GroupBy(entry => entry.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Model, StringComparer.Ordinal);

        var resolved = new List<TModel>(ids.Count);
        foreach (var id in ids) {
            if (byId.TryGetValue(id, out var model))
                resolved.Add(model);
        }

        if (resolved.Count == 0)
            return [];
        return resolved;
    }

    private static string IdOf<TModel>(TModel model) {
        if (model is AbstractModel abstractModel)
            return abstractModel.Id.Entry ?? "";
        return "";
    }
}
