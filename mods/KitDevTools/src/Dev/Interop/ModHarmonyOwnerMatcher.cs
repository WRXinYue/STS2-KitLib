using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KitLib.Abstractions.Host;
using KitLib.Abstractions.Modding;

namespace KitLib.Interop;

/// <summary>Maps scanned mod ids to Harmony patch owners and builds per-mod summaries.</summary>
public static class ModHarmonyOwnerMatcher {
    /// <summary>
    /// Mod-panel attribution includes every Harmony owner (no <see cref="HarmonySmartAnalysis.DefaultExcludedOwners"/>
    /// KitLib filter — that list is for conflict heuristics, not per-mod counts).
    /// </summary>
    private static HarmonySmartAnalysis.SmartAnalysisResult? AnalyzeForModPanel(out string? error) =>
        HarmonySmartAnalysis.Analyze(out error, excludedPatterns: null);

    public static ModHarmonyPatchStats? TryGetStats(string modId, string? installPath) {
        if (string.IsNullOrWhiteSpace(modId))
            return null;

        var smart = AnalyzeForModPanel(out _);
        if (smart == null)
            return null;

        var owners = smart.PatchesByOwner
            .Where(o => OwnerMatchesMod(o.Owner, modId, installPath))
            .ToList();
        if (owners.Count == 0)
            return new ModHarmonyPatchStats(0, 0, 0, Array.Empty<string>());

        var ownerIds = owners.Select(o => o.Owner).OrderBy(o => o, StringComparer.OrdinalIgnoreCase).ToList();
        var ops = owners.Sum(o => o.PatchCount);
        var methods = CountPatchedMethods(smart, modId, installPath);
        return new ModHarmonyPatchStats(ops, methods, ownerIds.Count, ownerIds);
    }

    public static string? BuildDetailReport(string modId, string? installPath) {
        if (string.IsNullOrWhiteSpace(modId))
            return null;

        var smart = AnalyzeForModPanel(out var err);
        if (smart == null)
            return err ?? "(Harmony analysis unavailable)";

        var stats = TryGetStats(modId, installPath);
        if (stats == null)
            return "(Harmony analysis unavailable)";

        var sb = new StringBuilder();
        sb.AppendLine($"Mod id: {modId}");
        if (!string.IsNullOrWhiteSpace(installPath))
            sb.AppendLine($"Install: {installPath}");
        sb.AppendLine();
        sb.AppendLine($"Patch operations: {stats.Value.PatchOperations}");
        sb.AppendLine($"Patched methods:  {stats.Value.PatchedMethods}");
        sb.AppendLine($"Harmony owners:   {stats.Value.HarmonyOwnerCount}");
        sb.AppendLine();

        if (stats.Value.OwnerIds.Count > 0) {
            sb.AppendLine("-- Owners --");
            foreach (var owner in stats.Value.OwnerIds) {
                var count = smart.PatchesByOwner
                    .Where(o => string.Equals(o.Owner, owner, StringComparison.OrdinalIgnoreCase))
                    .Select(o => o.PatchCount)
                    .FirstOrDefault();
                sb.AppendLine($"  {owner} ({count})");
            }
            sb.AppendLine();
        }

        sb.AppendLine("-- Patched methods --");
        var anyMethod = false;
        foreach (var typeInfo in smart.PatchesByDeclaringType) {
            foreach (var method in typeInfo.Methods) {
                var lines = method.Lines
                    .Where(l => OwnerMatchesMod(l.Owner, modId, installPath))
                    .ToList();
                if (lines.Count == 0)
                    continue;
                anyMethod = true;
                sb.AppendLine($"{typeInfo.DeclaringTypeFullName}.{method.MethodSignature}");
                foreach (var line in lines.OrderBy(l => l.HookKind).ThenBy(l => l.Priority).ThenBy(l => l.Owner))
                    sb.AppendLine($"  {line.HookKind} [{line.Priority}] {line.Owner} → {line.PatchMethodRef}");
                sb.AppendLine();
            }
        }

        if (!anyMethod)
            sb.AppendLine("  (none)");

        return sb.ToString().TrimEnd();
    }

    internal static bool OwnerMatchesMod(string owner, string modId, string? installPath) {
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(modId))
            return false;

        if (string.Equals(owner, modId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (owner.Contains(modId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (modId.Contains(owner, StringComparison.OrdinalIgnoreCase) && owner.Length >= 4)
            return true;

        if (string.Equals(modId, KitLibModuleIds.Core, StringComparison.OrdinalIgnoreCase)
            && owner.StartsWith("KitLib", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(installPath)) {
            var folder = Path.GetFileName(installPath.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!string.IsNullOrWhiteSpace(folder)
                && owner.Contains(folder, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static int CountPatchedMethods(
        HarmonySmartAnalysis.SmartAnalysisResult smart,
        string modId,
        string? installPath) {
        var count = 0;
        foreach (var typeInfo in smart.PatchesByDeclaringType) {
            foreach (var method in typeInfo.Methods) {
                if (method.Lines.Any(l => OwnerMatchesMod(l.Owner, modId, installPath)))
                    count++;
            }
        }
        return count;
    }
}
