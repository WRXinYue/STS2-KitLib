using System;
using System.Collections.Generic;
using System.Linq;

namespace KitLib.Abstractions.Host;

/// <summary>Pure load policy for bundled KitLib satellite modules (settings + dependency rules).</summary>
public static class SatelliteModuleLoadPolicy {
    public const string ProfileMinimal = "Minimal";
    public const string ProfileStandard = "Standard";
    public const string ProfileFull = "Full";
    public const string ProfileCustom = "Custom";

    public static readonly string[] KnownProfiles = [ProfileMinimal, ProfileStandard, ProfileFull, ProfileCustom];

    public sealed record ModuleInfo(string Id, bool AlwaysOn, string[] Requires);

    public const string ModulesSubdir = "modules";

    public static readonly ModuleInfo[] Modules = [
        new(KitLibModuleIds.User, AlwaysOn: true, Requires: []),
        new(KitLibModuleIds.ModPanel, AlwaysOn: true, Requires: []),
        new(KitLibModuleIds.Panel, AlwaysOn: false, Requires: []),
        new(KitLibModuleIds.Ai, AlwaysOn: false, Requires: []),
        new(KitLibModuleIds.Cheat, AlwaysOn: false, Requires: [KitLibModuleIds.Panel]),
        new(KitLibModuleIds.Dev, AlwaysOn: false, Requires: [KitLibModuleIds.Panel]),
    ];

    static readonly HashSet<string> ToggleableModuleIds = new(
        Modules.Where(m => !m.AlwaysOn).Select(m => m.Id),
        StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, bool> GetPreset(string profile) {
        var normalized = NormalizeProfile(profile);
        var toggles = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in Modules.Where(m => !m.AlwaysOn))
            toggles[module.Id] = normalized switch {
                ProfileMinimal => false,
                ProfileStandard => module.Id == KitLibModuleIds.Panel,
                ProfileFull => true,
                _ => module.Id == KitLibModuleIds.Panel,
            };
        return toggles;
    }

    public static IReadOnlyDictionary<string, bool> ResolveEnabled(
        string profile,
        IReadOnlyDictionary<string, bool>? userToggles) {
        var resolved = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var preset = NormalizeProfile(profile) == ProfileCustom && userToggles != null && userToggles.Count > 0
            ? userToggles
            : GetPreset(profile);

        foreach (var module in Modules) {
            if (module.AlwaysOn) {
                resolved[module.Id] = true;
                continue;
            }

            resolved[module.Id] = preset.TryGetValue(module.Id, out var enabled) && enabled;
        }

        ApplyDependencies(resolved);
        return resolved;
    }

    public static bool ShouldLoad(string moduleId, IReadOnlyDictionary<string, bool> resolved, bool dllExists) {
        if (string.IsNullOrWhiteSpace(moduleId))
            return false;
        if (!dllExists)
            return false;
        return resolved.TryGetValue(moduleId, out var enabled) && enabled;
    }

    public static void ApplyDependencyRulesToToggles(Dictionary<string, bool> toggles, string moduleId, bool enabled) {
        if (!ToggleableModuleIds.Contains(moduleId))
            return;

        toggles[moduleId] = enabled;

        if (!enabled && string.Equals(moduleId, KitLibModuleIds.Panel, StringComparison.OrdinalIgnoreCase)) {
            toggles[KitLibModuleIds.Cheat] = false;
            toggles[KitLibModuleIds.Dev] = false;
            return;
        }

        if (enabled && (string.Equals(moduleId, KitLibModuleIds.Cheat, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(moduleId, KitLibModuleIds.Dev, StringComparison.OrdinalIgnoreCase)))
            toggles[KitLibModuleIds.Panel] = true;
    }

    public static string DetectMatchingProfile(IReadOnlyDictionary<string, bool> toggles) {
        foreach (var profile in new[] { ProfileMinimal, ProfileStandard, ProfileFull }) {
            if (TogglesMatchPreset(toggles, GetPreset(profile)))
                return profile;
        }

        return ProfileCustom;
    }

    public static bool IsToggleable(string moduleId) => ToggleableModuleIds.Contains(moduleId);

    public static bool TryGetModule(string moduleId, out ModuleInfo module) {
        foreach (var candidate in Modules) {
            if (!string.Equals(candidate.Id, moduleId, StringComparison.OrdinalIgnoreCase))
                continue;
            module = candidate;
            return true;
        }

        module = default!;
        return false;
    }

    public static string GetRelativeDllPath(string moduleId) => $"{ModulesSubdir}/{moduleId}.dll";

    public static IReadOnlyList<string> GetDependents(string moduleId) {
        var dependents = new List<string>();
        foreach (var module in Modules) {
            if (module.Requires.Any(required =>
                    string.Equals(required, moduleId, StringComparison.OrdinalIgnoreCase)))
                dependents.Add(module.Id);
        }
        return dependents;
    }

    static void ApplyDependencies(Dictionary<string, bool> resolved) {
        foreach (var module in Modules.Where(m => m.Requires.Length > 0)) {
            if (!resolved.TryGetValue(module.Id, out var enabled) || !enabled)
                continue;
            foreach (var required in module.Requires) {
                if (resolved.TryGetValue(required, out var reqEnabled) && reqEnabled)
                    continue;
                resolved[module.Id] = false;
                break;
            }
        }
    }

    static bool TogglesMatchPreset(IReadOnlyDictionary<string, bool> toggles, IReadOnlyDictionary<string, bool> preset) {
        foreach (var module in Modules.Where(m => !m.AlwaysOn)) {
            var a = toggles.TryGetValue(module.Id, out var ta) && ta;
            var b = preset.TryGetValue(module.Id, out var tb) && tb;
            if (a != b)
                return false;
        }

        return true;
    }

    static string NormalizeProfile(string? profile) {
        if (string.IsNullOrWhiteSpace(profile))
            return ProfileStandard;
        foreach (var known in KnownProfiles) {
            if (string.Equals(known, profile, StringComparison.OrdinalIgnoreCase))
                return known;
        }

        return ProfileStandard;
    }
}
