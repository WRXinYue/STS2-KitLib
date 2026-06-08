using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Godot;
using HarmonyLib;

namespace KitLib.Interop;

/// <summary>
/// Builds a UTF-8 text report of per-method Harmony hooks (owner, priority), matching common mod-framework dump layouts.
/// </summary>
public static class HarmonyPatchReportBuilder {
    private static string? _cachedReport;

    /// <summary>Discard the cached full report so the next <see cref="BuildReport"/> call re-scans Harmony.</summary>
    public static void InvalidateCache() => _cachedReport = null;

    /// <summary>
    /// Full report text, or empty with <paramref name="error"/> set on failure.
    /// Result is cached until <see cref="InvalidateCache"/> is called.
    /// </summary>
    public static string BuildReport(out string? error) {
        if (_cachedReport != null) {
            error = null;
            return _cachedReport;
        }

        error = null;
        try {
            using var sw = new StringWriter();
            WriteReport(sw);
            _cachedReport = sw.ToString();
            return _cachedReport;
        }
        catch (Exception ex) {
            error = ex.Message;
            return "";
        }
    }

    private static void WriteReport(TextWriter streamWriter) {
        streamWriter.WriteLine("=======================================================");
        streamWriter.WriteLine("===          Harmony Patch Dump Report             ===");
        streamWriter.WriteLine("=======================================================");
        streamWriter.WriteLine($"Generated at: {DateTime.Now:O}");
        streamWriter.WriteLine($"User data dir: {OS.GetUserDataDir()}");
        streamWriter.WriteLine("=======================================================");
        streamWriter.WriteLine();

        var allPatchedMethods = Harmony.GetAllPatchedMethods()
            .OrderBy(m => m.DeclaringType?.FullName ?? "Unknown")
            .ThenBy(m => m.Name)
            .ToList();

        var methodCount = 0;
        var totalPrefixes = 0;
        var totalPostfixes = 0;
        var totalTranspilers = 0;
        var totalFinalizers = 0;

        foreach (var patchedMethod in allPatchedMethods) {
            methodCount++;
            var counts = LogPatchedMethodInfo(patchedMethod, streamWriter);
            totalPrefixes += counts.prefixes;
            totalPostfixes += counts.postfixes;
            totalTranspilers += counts.transpilers;
            totalFinalizers += counts.finalizers;
            streamWriter.WriteLine();
        }

        streamWriter.WriteLine("=======================================================");
        streamWriter.WriteLine("===                   Summary                      ===");
        streamWriter.WriteLine("=======================================================");
        streamWriter.WriteLine($"Total Patched Methods:  {methodCount}");
        streamWriter.WriteLine($"  - Prefix patches:     {totalPrefixes}");
        streamWriter.WriteLine($"  - Postfix patches:    {totalPostfixes}");
        streamWriter.WriteLine($"  - Transpiler patches: {totalTranspilers}");
        streamWriter.WriteLine($"  - Finalizer patches:  {totalFinalizers}");
        streamWriter.WriteLine(
            $"  - Total patches:      {totalPrefixes + totalPostfixes + totalTranspilers + totalFinalizers}");
        streamWriter.WriteLine("=======================================================");
    }

    private static (int prefixes, int postfixes, int transpilers, int finalizers) LogPatchedMethodInfo(
        MethodBase methodBase, TextWriter streamWriter) {
        var patchInfo = Harmony.GetPatchInfo(methodBase);
        if (patchInfo == null) return (0, 0, 0, 0);

        var declaringType = methodBase.DeclaringType?.FullName ?? "Unknown";
        var methodSignature = GetMethodSignature(methodBase);
        var returnType = methodBase is MethodInfo mi ? mi.ReturnType.Name : "void";

        streamWriter.WriteLine($"┌─ [{declaringType}]");
        streamWriter.WriteLine($"│  Method: {returnType} {methodSignature}");
        streamWriter.WriteLine("│");

        var prefixCount = 0;
        var postfixCount = 0;
        var transpilerCount = 0;
        var finalizerCount = 0;

        if (patchInfo.Prefixes.Count > 0) {
            streamWriter.WriteLine($"│  ├─ Prefixes ({patchInfo.Prefixes.Count}):");
            foreach (var patch in patchInfo.Prefixes.OrderBy(p => p.priority).ThenBy(p => p.owner)) {
                streamWriter.WriteLine($"│  │  {FormatPatchInfo(patch)}");
                prefixCount++;
            }
        }

        if (patchInfo.Postfixes.Count > 0) {
            streamWriter.WriteLine($"│  ├─ Postfixes ({patchInfo.Postfixes.Count}):");
            foreach (var patch in patchInfo.Postfixes.OrderBy(p => p.priority).ThenBy(p => p.owner)) {
                streamWriter.WriteLine($"│  │  {FormatPatchInfo(patch)}");
                postfixCount++;
            }
        }

        if (patchInfo.Transpilers.Count > 0) {
            streamWriter.WriteLine($"│  ├─ Transpilers ({patchInfo.Transpilers.Count}):");
            foreach (var patch in patchInfo.Transpilers.OrderBy(p => p.priority).ThenBy(p => p.owner)) {
                streamWriter.WriteLine($"│  │  {FormatPatchInfo(patch)}");
                transpilerCount++;
            }
        }

        if (patchInfo.Finalizers.Count > 0) {
            streamWriter.WriteLine($"│  └─ Finalizers ({patchInfo.Finalizers.Count}):");
            foreach (var patch in patchInfo.Finalizers.OrderBy(p => p.priority).ThenBy(p => p.owner)) {
                streamWriter.WriteLine($"│     {FormatPatchInfo(patch)}");
                finalizerCount++;
            }
        }

        streamWriter.WriteLine("└─────────────────────────────────────────────────────────────────");

        return (prefixCount, postfixCount, transpilerCount, finalizerCount);
    }

    private static string GetMethodSignature(MethodBase methodBase) {
        var parameters = methodBase.GetParameters();
        var paramString = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
        return $"{methodBase.Name}({paramString})";
    }

    private static string FormatPatchInfo(Patch patch) {
        var sb = new StringBuilder();
        sb.Append($"├─ [Priority: {patch.priority}] ");
        sb.Append($"[{patch.owner}] ");
        var patchClass = patch.PatchMethod.DeclaringType?.FullName ?? "Unknown";
        var patchMethodName = patch.PatchMethod.Name;
        sb.Append($"{patchClass}.{patchMethodName}");

        try {
            var moduleName = Path.GetFileName(patch.PatchMethod.Module.FullyQualifiedName);
            if (!string.IsNullOrEmpty(moduleName) && moduleName != "<Unknown>")
                sb.Append($" (from {moduleName})");
        }
        catch {
            // ignored
        }

        return sb.ToString();
    }
}
