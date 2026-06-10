using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace KitLib;

/// <summary>Applies Harmony patches from a single module assembly once per process.</summary>
public static class KitLibHarmony {
    static readonly HashSet<string> Applied = new(StringComparer.OrdinalIgnoreCase);

    public static void Apply(Assembly moduleAssembly, string harmonyId) {
        ArgumentNullException.ThrowIfNull(moduleAssembly);
        if (string.IsNullOrWhiteSpace(harmonyId))
            throw new ArgumentException("Harmony id is required.", nameof(harmonyId));
        if (!Applied.Add(harmonyId))
            return;

        var harmony = new Harmony(harmonyId);
        var patchTypes = AccessTools.GetTypesFromAssembly(moduleAssembly)
            .Where(t => t.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length > 0)
            .ToList();

        int applied = 0;
        int skipped = 0;
        foreach (var type in patchTypes) {
            try {
                harmony.CreateClassProcessor(type).Patch();
                applied++;
            }
            catch (Exception ex) {
                skipped++;
                MainFile.Logger.Warn($"KitLib Harmony skipped patch type {type.FullName}: {ex.Message}");
            }
        }

        MainFile.Logger.Info(
            $"KitLib Harmony patches applied: {harmonyId} ({applied} types, {skipped} skipped).");
    }
}
