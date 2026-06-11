using System;
using System.Collections.Generic;
using KitLib.Abstractions.Modding;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.Modding;

/// <summary><see cref="IModRegistry"/> backed by <c>ModManager.Mods</c>.</summary>
public sealed class Sts2ModRegistry : IModRegistry {
    public static IModRegistry Default { get; } = new Sts2ModRegistry();

    private Sts2ModRegistry() { }

    public IReadOnlyList<KitLibModEntry> GetAllEntries() {
        var mods = ModManager.Mods;
        if (mods.Count == 0)
            return Array.Empty<KitLibModEntry>();

        var settings = Sts2ModLoadSettings.TryGetModSettings();
        var list = new List<KitLibModEntry>(mods.Count);
        foreach (var mod in mods) {
            var entry = Map(mod, settings);
            if (entry != null)
                list.Add(entry.Value);
        }
        return list;
    }

    public KitLibModEntry? TryGet(string id, ModEntrySource source) {
        if (string.IsNullOrEmpty(id))
            return null;
        var stsSource = ToStsSource(source);
        foreach (var mod in ModManager.Mods) {
            if (!string.Equals(mod.manifest?.id, id, StringComparison.OrdinalIgnoreCase))
                continue;
            if (mod.modSource != stsSource)
                continue;
            return Map(mod, Sts2ModLoadSettings.TryGetModSettings());
        }
        return null;
    }

    internal static KitLibModEntry? Map(Mod mod, ModSettings? settings) {
        var man = mod.manifest;
        if (man == null || string.IsNullOrEmpty(man.id))
            return null;
        var name = KitLibCompatDisplay.FormatSidebarDisplayName(
            string.IsNullOrEmpty(man.name) ? man.id : man.name,
            mod.path);
        var ver = man.version ?? "";
        var enabled = settings == null || !settings.IsModDisabled(man.id, mod.modSource);
        return new KitLibModEntry(
            man.id,
            name,
            ver,
            Sts2ModCatalogDeps.CopyDependencies(man),
            MapLoadStatus(mod.state),
            MapSource(mod.modSource),
            enabled);
    }

    internal static ModEntryLoadStatus MapLoadStatus(ModLoadState state) => state switch {
        ModLoadState.None => ModEntryLoadStatus.None,
        ModLoadState.Loaded => ModEntryLoadStatus.Loaded,
        ModLoadState.Failed => ModEntryLoadStatus.Failed,
        ModLoadState.Disabled => ModEntryLoadStatus.Disabled,
        ModLoadState.AddedAtRuntime => ModEntryLoadStatus.AddedAtRuntime,
        _ => ModEntryLoadStatus.None,
    };

    internal static ModEntrySource MapSource(ModSource source) => source switch {
        ModSource.SteamWorkshop => ModEntrySource.SteamWorkshop,
        _ => ModEntrySource.ModsDirectory,
    };

    internal static ModSource ToStsSource(ModEntrySource source) => source switch {
        ModEntrySource.SteamWorkshop => ModSource.SteamWorkshop,
        _ => ModSource.ModsDirectory,
    };
}
