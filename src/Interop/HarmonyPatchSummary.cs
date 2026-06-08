using HarmonyLib;

namespace KitLib.Interop;

/// <summary>
/// Process-wide Harmony stats (<see cref="Harmony.GetAllPatchedMethods"/>).
/// </summary>
public static class HarmonyPatchSummary {
    public readonly record struct Stats(
        int PatchedMethodCount,
        int PrefixCount,
        int PostfixCount,
        int TranspilerCount,
        int FinalizerCount) {
        /// <summary>Sum of all patch hooks (can exceed patched methods when multiple hooks target the same method).</summary>
        public int TotalPatchOperations =>
            PrefixCount + PostfixCount + TranspilerCount + FinalizerCount;
    }

    /// <summary>Returns aggregated patch counts, or zeros if Harmony is unavailable.</summary>
    public static Stats Aggregate() {
        try {
            var prefixes = 0;
            var postfixes = 0;
            var transpilers = 0;
            var finalizers = 0;
            var methodCount = 0;

            foreach (var m in Harmony.GetAllPatchedMethods()) {
                methodCount++;
                var info = Harmony.GetPatchInfo(m);
                if (info == null)
                    continue;
                prefixes += info.Prefixes.Count;
                postfixes += info.Postfixes.Count;
                transpilers += info.Transpilers.Count;
                finalizers += info.Finalizers.Count;
            }

            return new Stats(methodCount, prefixes, postfixes, transpilers, finalizers);
        }
        catch {
            return new Stats(0, 0, 0, 0, 0);
        }
    }
}
