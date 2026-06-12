using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace KitLib;

/// <summary>Applies Harmony patches from a single module assembly once per process.</summary>
public static class KitLibHarmony {
    static readonly HashSet<string> Applied = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Harmony> Instances = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsApplied(string harmonyId) =>
        !string.IsNullOrWhiteSpace(harmonyId) && Applied.Contains(harmonyId);

    public static Harmony GetOrCreate(string harmonyId) {
        if (string.IsNullOrWhiteSpace(harmonyId))
            throw new ArgumentException("Harmony id is required.", nameof(harmonyId));
        if (!Instances.TryGetValue(harmonyId, out var harmony)) {
            harmony = new Harmony(harmonyId);
            Instances[harmonyId] = harmony;
        }
        return harmony;
    }

    public static void Apply(Assembly moduleAssembly, string harmonyId) {
        ArgumentNullException.ThrowIfNull(moduleAssembly);
        if (string.IsNullOrWhiteSpace(harmonyId))
            throw new ArgumentException("Harmony id is required.", nameof(harmonyId));
        if (IsApplied(harmonyId))
            return;

        var harmony = GetOrCreate(harmonyId);
        List<Type> patchTypes;
        try {
            patchTypes = AccessTools.GetTypesFromAssembly(moduleAssembly)
                .Where(HasHarmonyPatch)
                .ToList();
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib Harmony could not scan {harmonyId}: {ex.Message}");
            return;
        }

        var appliedTypes = new List<string>();
        var skipped = new List<(string Type, string Reason)>();
        if (patchTypes.Count == 0) {
            try {
                harmony.PatchAll(moduleAssembly);
                Applied.Add(harmonyId);
                MainFile.Logger.Info($"KitLib Harmony patches applied: {harmonyId} (PatchAll).");
                return;
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"KitLib Harmony PatchAll failed for {harmonyId}: {ex.Message}");
                return;
            }
        }

        foreach (var type in patchTypes) {
            try {
                harmony.CreateClassProcessor(type).Patch();
                appliedTypes.Add(type.FullName ?? type.Name);
            }
            catch (Exception ex) {
                skipped.Add((type.FullName ?? type.Name, ex.Message));
                MainFile.Logger.Warn($"KitLib Harmony skipped patch type {type.FullName}: {ex.Message}");
            }
        }

        Applied.Add(harmonyId);

        MainFile.Logger.Info(
            $"KitLib Harmony patches applied: {harmonyId} ({appliedTypes.Count} types, {skipped.Count} skipped).");
        if (skipped.Count > 0) {
            MainFile.Logger.Warn(
                $"KitLib Harmony {harmonyId}: {skipped.Count} patch type(s) skipped — search session.log for " +
                "'Harmony skipped patch type' or module health errors.");
        }
    }

    static bool HasHarmonyPatch(Type type) {
        foreach (var attr in type.GetCustomAttributes(inherit: false)) {
            var attrType = attr.GetType();
            if (attrType == typeof(HarmonyPatch) || attrType.Name is "HarmonyPatch" or "HarmonyPatchAttribute")
                return true;
        }

        return false;
    }
}
