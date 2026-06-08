using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using KitLib;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Modding;

internal readonly record struct ContentModSource(string Key, string DisplayLabel, string? ModId) {
    public const string GameKey = "Game";
    public const string AssemblyKeyPrefix = "asm:";

    public bool IsGame => Key == GameKey;
}

internal static class ContentModResolver {
    private static readonly ConcurrentDictionary<Type, ContentModSource> _cache = new();

    public static ContentModSource Resolve(AbstractModel model)
        => Resolve(model.GetType());

    public static ContentModSource Resolve(Type modelType) {
        if (modelType == null)
            return GameSource();

        return _cache.GetOrAdd(modelType, ResolveUncached);
    }

    private static ContentModSource ResolveUncached(Type modelType) {
        var asmName = modelType.Assembly.GetName().Name;
        if (string.IsNullOrEmpty(asmName))
            return GameSource();

        if (ModAssemblyLookup.IsGameCoreAssembly(asmName))
            return GameSource();

        if (ModAssemblyLookup.TryGetByAssemblySimpleName(asmName, out var mod))
            return new ContentModSource(mod.Id, mod.DisplayName, mod.Id);

        return UnknownAssemblySource(asmName);
    }

    public static ContentModSource GameSource()
        => new(ContentModSource.GameKey, I18N.T("browser.modSource.game", "Game"), null);

    public static ContentModSource UnknownAssemblySource(string assemblySimpleName)
        => new(
            ContentModSource.AssemblyKeyPrefix + assemblySimpleName,
            string.Format(I18N.T("browser.modSource.unknown", "Unknown ({0})"), assemblySimpleName),
            null);

    public static List<(string key, string label)> BuildFilterEntries(IEnumerable<AbstractModel> items) {
        var byKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in items) {
            var src = Resolve(item);
            byKey.TryAdd(src.Key, src.DisplayLabel);
        }

        var game = new List<(string key, string label)>();
        var mods = new List<(string key, string label)>();
        var unknown = new List<(string key, string label)>();

        foreach (var (key, label) in byKey) {
            if (key == ContentModSource.GameKey)
                game.Add((key, label));
            else if (key.StartsWith(ContentModSource.AssemblyKeyPrefix, StringComparison.Ordinal))
                unknown.Add((key, label));
            else
                mods.Add((key, label));
        }

        mods.Sort((a, b) => string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase));
        unknown.Sort((a, b) => string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase));

        var result = new List<(string key, string label)>(game.Count + mods.Count + unknown.Count);
        result.AddRange(game);
        result.AddRange(mods);
        result.AddRange(unknown);
        return result;
    }

    public static bool MatchesModSourceFilter(
        ContentModSource src,
        HashSet<string> active,
        HashSet<string> excluded) {
        if (excluded.Contains(src.Key))
            return false;
        if (active.Count == 0)
            return true;
        return active.Contains(src.Key);
    }
}
