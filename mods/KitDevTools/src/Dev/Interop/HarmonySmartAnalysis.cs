using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace KitLib.Interop;

/// <summary>
/// Heuristic Harmony insights: owner totals, busiest methods, multi-owner targets, and risk-style hints.
/// </summary>
public static class HarmonySmartAnalysis {
    public const int MaxBusiestMethods = 28;
    public const int MaxMultiOwnerMethods = 36;
    public const int HeavyPrefixThreshold = 4;
    public const int HeavyPostfixThreshold = 4;
    public const int MaxTranspilerRiskRows = 32;
    public const int MaxSamePriorityRows = 32;
    public const int MaxHeavyHookRows = 24;

    public sealed record MethodHotspot(
        string DeclaringType,
        string MethodSignature,
        int TotalHooks,
        int Prefix,
        int Postfix,
        int Transpiler,
        int Finalizer);

    public sealed record MultiOwnerRow(
        string DeclaringType,
        string MethodSignature,
        int DistinctOwners,
        int TotalHooks,
        IReadOnlyList<string> OwnersSorted);

    public sealed record TranspilerStackRisk(
        string DeclaringType,
        string MethodSignature,
        int TranspilerCount,
        IReadOnlyList<string> PatchLines);

    public sealed record SamePriorityRisk(
        string HookKind,
        int Priority,
        string DeclaringType,
        string MethodSignature,
        IReadOnlyList<string> PatchLines);

    public sealed record HeavyHookRisk(
        string HookKind,
        int Count,
        string DeclaringType,
        string MethodSignature,
        IReadOnlyList<string> PatchLines);

    /// <summary>One patch line on an original method (Harmony owner + patch method).</summary>
    public sealed record PatchLine(
        string HookKind,
        string Owner,
        int Priority,
        string PatchMethodRef);

    public sealed record MethodPatchDetail(
        string MethodSignature,
        IReadOnlyList<PatchLine> Lines);

    /// <summary>All patched methods that share the same declaring type (CLR full name).</summary>
    public sealed record DeclaringTypePatchInfo(
        string DeclaringTypeFullName,
        int TotalPatchOperations,
        int DistinctOwnerCount,
        IReadOnlyList<MethodPatchDetail> Methods);

    public sealed record SmartAnalysisResult(
        int PatchedMethodCount,
        int TotalPatchOperations,
        int DistinctOwnerCount,
        IReadOnlyList<(string Owner, int PatchCount)> PatchesByOwner,
        IReadOnlyList<MethodHotspot> BusiestMethods,
        IReadOnlyList<MultiOwnerRow> MultiOwnerMethods,
        IReadOnlyList<TranspilerStackRisk> TranspilerStackRisks,
        IReadOnlyList<SamePriorityRisk> SamePriorityRisks,
        IReadOnlyList<HeavyHookRisk> HeavyHookRisks,
        IReadOnlyList<DeclaringTypePatchInfo> PatchesByDeclaringType);

    /// <summary>
    /// Default patterns excluded from analysis (KitLib modules and framework dependency).
    /// Patterns ending with <c>*</c> match any owner that starts with the prefix before the asterisk.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultExcludedOwners = ["KitLib*", "com.ritsukage.sts2-RitsuLib.*"];

    private static SmartAnalysisResult? _cachedResult;
    private static string? _cachedFormattedReport;
    private static string _cachedPatternsKey = "";

    /// <summary>Discard the cached analysis and formatted report so the next calls re-scan Harmony.</summary>
    public static void InvalidateCache() {
        _cachedResult = null;
        _cachedFormattedReport = null;
        _cachedPatternsKey = "";
    }

    private static string PatternsKey(IReadOnlyList<string>? patterns) =>
        patterns == null || patterns.Count == 0 ? "" : string.Join(",", patterns);

    /// <summary>
    /// Aggregates patch graph; <paramref name="error"/> set on failure.
    /// Patches whose owner matches any pattern in <paramref name="excludedPatterns"/> are invisible to all
    /// statistics and risk checks. Methods where every patch is excluded are omitted entirely.
    /// Patterns ending with <c>*</c> match by prefix; all others are exact matches.
    /// </summary>
    public static SmartAnalysisResult? Analyze(out string? error, IReadOnlyList<string>? excludedPatterns = null) {
        error = null;
        var key = PatternsKey(excludedPatterns);
        if (_cachedResult != null && _cachedPatternsKey == key)
            return _cachedResult;

        try {
            var allMethods = Harmony.GetAllPatchedMethods()
                .OrderBy(m => m.DeclaringType?.FullName ?? "")
                .ThenBy(m => m.Name)
                .ToList();

            var byOwner = new Dictionary<string, int>(StringComparer.Ordinal);
            var perMethod = new List<(MethodBase Method, MethodHotspot Stats, HashSet<string> Owners)>();
            var totalOps = 0;

            // Methods that survive the owner filter (have ≥1 non-excluded patch).
            var includedMethods = new List<MethodBase>();

            foreach (var m in allMethods) {
                var info = Harmony.GetPatchInfo(m);
                if (info == null) continue;

                var owners = new HashSet<string>(StringComparer.Ordinal);
                var px = 0; var po = 0; var tr = 0; var fi = 0;

                void Accumulate(IReadOnlyCollection<Patch> patches, ref int counter) {
                    foreach (var p in patches) {
                        var o = p.owner ?? "?";
                        if (IsExcluded(o, excludedPatterns)) continue;
                        owners.Add(o);
                        byOwner[o] = byOwner.GetValueOrDefault(o) + 1;
                        totalOps++;
                        counter++;
                    }
                }

                Accumulate(info.Prefixes, ref px);
                Accumulate(info.Postfixes, ref po);
                Accumulate(info.Transpilers, ref tr);
                Accumulate(info.Finalizers, ref fi);

                if (px + po + tr + fi == 0) continue; // all patches from excluded owners

                includedMethods.Add(m);
                var dt = m.DeclaringType?.FullName ?? "Unknown";
                var sig = GetMethodSignature(m);
                perMethod.Add((m, new MethodHotspot(dt, sig, px + po + tr + fi, px, po, tr, fi), owners));
            }

            var busiest = perMethod
                .Select(x => x.Stats)
                .OrderByDescending(s => s.TotalHooks)
                .ThenBy(s => s.DeclaringType)
                .Take(MaxBusiestMethods)
                .ToList();

            var multi = perMethod
                .Where(x => x.Owners.Count >= 2)
                .Select(x => new MultiOwnerRow(
                    x.Stats.DeclaringType,
                    x.Stats.MethodSignature,
                    x.Owners.Count,
                    x.Stats.TotalHooks,
                    x.Owners.OrderBy(o => o).ToList()))
                .OrderByDescending(r => r.DistinctOwners)
                .ThenByDescending(r => r.TotalHooks)
                .Take(MaxMultiOwnerMethods)
                .ToList();

            var byOwnerList = byOwner
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();

            var transpilerRisks = new List<TranspilerStackRisk>();
            var samePriRisks = new List<SamePriorityRisk>();
            var heavyRisks = new List<HeavyHookRisk>();

            foreach (var m in includedMethods) {
                var info = Harmony.GetPatchInfo(m);
                if (info == null) continue;

                var fPrefixes = FilteredPatches(info.Prefixes, excludedPatterns);
                var fPostfixes = FilteredPatches(info.Postfixes, excludedPatterns);
                var fTranspilers = FilteredPatches(info.Transpilers, excludedPatterns);
                var fFinalizers = FilteredPatches(info.Finalizers, excludedPatterns);

                var dt = m.DeclaringType?.FullName ?? "Unknown";
                var sig = GetMethodSignature(m);

                if (fTranspilers.Count >= 2) {
                    var lines = fTranspilers
                        .OrderBy(p => p.priority)
                        .ThenBy(p => p.owner)
                        .Select(FormatPatchLine)
                        .ToList();
                    transpilerRisks.Add(new TranspilerStackRisk(dt, sig, fTranspilers.Count, lines));
                }

                AddSamePriorityRisks(fPrefixes, "Prefix", dt, sig, samePriRisks);
                AddSamePriorityRisks(fPostfixes, "Postfix", dt, sig, samePriRisks);
                AddSamePriorityRisks(fTranspilers, "Transpiler", dt, sig, samePriRisks);
                AddSamePriorityRisks(fFinalizers, "Finalizer", dt, sig, samePriRisks);

                if (fPrefixes.Count >= HeavyPrefixThreshold) {
                    var lines = fPrefixes
                        .OrderBy(p => p.priority)
                        .ThenBy(p => p.owner)
                        .Select(FormatPatchLine)
                        .ToList();
                    heavyRisks.Add(new HeavyHookRisk("Prefix", fPrefixes.Count, dt, sig, lines));
                }

                if (fPostfixes.Count >= HeavyPostfixThreshold) {
                    var lines = fPostfixes
                        .OrderBy(p => p.priority)
                        .ThenBy(p => p.owner)
                        .Select(FormatPatchLine)
                        .ToList();
                    heavyRisks.Add(new HeavyHookRisk("Postfix", fPostfixes.Count, dt, sig, lines));
                }
            }

            transpilerRisks = transpilerRisks
                .OrderByDescending(r => r.TranspilerCount)
                .ThenBy(r => r.DeclaringType)
                .Take(MaxTranspilerRiskRows)
                .ToList();

            samePriRisks = samePriRisks
                .OrderByDescending(r => r.PatchLines.Count)
                .ThenBy(r => r.HookKind)
                .Take(MaxSamePriorityRows)
                .ToList();

            heavyRisks = heavyRisks
                .OrderByDescending(r => r.Count)
                .ThenBy(r => r.HookKind)
                .Take(MaxHeavyHookRows)
                .ToList();

            var byDeclaringType = BuildPatchesByDeclaringType(includedMethods, excludedPatterns);

            _cachedResult = new SmartAnalysisResult(
                includedMethods.Count,
                totalOps,
                byOwner.Count,
                byOwnerList,
                busiest,
                multi,
                transpilerRisks,
                samePriRisks,
                heavyRisks,
                byDeclaringType);
            _cachedPatternsKey = key;
            return _cachedResult;
        }
        catch (Exception ex) {
            error = ex.Message;
            return null;
        }
    }

    private static List<Patch> FilteredPatches(IReadOnlyCollection<Patch> patches, IReadOnlyList<string>? excluded) {
        if (excluded == null || excluded.Count == 0) return [.. patches];
        return patches.Where(p => !IsExcluded(p.owner ?? "?", excluded)).ToList();
    }

    /// <summary>
    /// Returns true when <paramref name="owner"/> matches any pattern in <paramref name="patterns"/>.
    /// Patterns ending with <c>*</c> are prefix-matched; others require exact equality.
    /// </summary>
    private static bool IsExcluded(string owner, IReadOnlyList<string>? patterns) {
        if (patterns == null) return false;
        foreach (var p in patterns) {
            if (p.EndsWith('*')) {
                if (owner.StartsWith(p[..^1], StringComparison.Ordinal)) return true;
            }
            else {
                if (owner.Equals(p, StringComparison.Ordinal)) return true;
            }
        }

        return false;
    }

    private static void AddSamePriorityRisks(
        IReadOnlyCollection<Patch> patches,
        string hookKind,
        string dt,
        string sig,
        List<SamePriorityRisk> sink) {
        foreach (var g in patches.GroupBy(p => p.priority)) {
            var distinctOwners = g.Select(p => p.owner ?? "?").Distinct().Count();
            if (distinctOwners < 2) continue;

            var lines = g
                .OrderBy(p => p.owner)
                .ThenBy(p => p.PatchMethod.Name)
                .Select(FormatPatchLine)
                .ToList();
            sink.Add(new SamePriorityRisk(hookKind, g.Key, dt, sig, lines));
        }
    }

    private static string FormatPatchLine(Patch p) {
        var o = p.owner ?? "?";
        var pm = p.PatchMethod;
        var cls = pm.DeclaringType?.FullName ?? "?";
        return $"{o}  pri={p.priority}  {cls}.{pm.Name}";
    }

    private static string PatchMethodRef(Patch p) {
        var pm = p.PatchMethod;
        var cls = pm.DeclaringType?.FullName ?? "?";
        return $"{cls}.{pm.Name}";
    }

    private static IReadOnlyList<DeclaringTypePatchInfo> BuildPatchesByDeclaringType(
        List<MethodBase> methods, IReadOnlyList<string>? excludedOwners = null) {
        var groups = new Dictionary<string, List<MethodBase>>(StringComparer.Ordinal);
        foreach (var m in methods) {
            var key = m.DeclaringType?.FullName ?? "Unknown";
            if (!groups.TryGetValue(key, out var list)) {
                list = new List<MethodBase>();
                groups[key] = list;
            }

            list.Add(m);
        }

        var result = new List<DeclaringTypePatchInfo>();
        foreach (var kv in groups.OrderBy(x => x.Key, StringComparer.Ordinal)) {
            var typeName = kv.Key;
            var inType = kv.Value;
            var owners = new HashSet<string>(StringComparer.Ordinal);
            var methodDetails = new List<MethodPatchDetail>();
            var totalOps = 0;

            foreach (var m in inType.OrderBy(x => x.Name).ThenBy(x => x.MetadataToken)) {
                var info = Harmony.GetPatchInfo(m);
                if (info == null) continue;

                var lines = new List<PatchLine>();

                void AddPatches(IReadOnlyCollection<Patch> patches, string hookKind) {
                    foreach (var p in patches.OrderBy(x => x.priority).ThenBy(x => x.owner)) {
                        var o = p.owner ?? "?";
                        if (IsExcluded(o, excludedOwners)) continue;
                        owners.Add(o);
                        totalOps++;
                        lines.Add(new PatchLine(hookKind, o, p.priority, PatchMethodRef(p)));
                    }
                }

                AddPatches(info.Prefixes, "Prefix");
                AddPatches(info.Postfixes, "Postfix");
                AddPatches(info.Transpilers, "Transpiler");
                AddPatches(info.Finalizers, "Finalizer");

                if (lines.Count > 0)
                    methodDetails.Add(new MethodPatchDetail(GetMethodSignature(m), lines));
            }

            if (methodDetails.Count > 0)
                result.Add(new DeclaringTypePatchInfo(typeName, totalOps, owners.Count, methodDetails));
        }

        return result;
    }

    private static string GetMethodSignature(MethodBase methodBase) {
        var parameters = methodBase.GetParameters();
        var paramString = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
        return $"{methodBase.Name}({paramString})";
    }

    /// <summary>Plain-text report for UI (section titles localized by caller).</summary>
    public static string FormatReport(
        SmartAnalysisResult r,
        string title,
        string secRisk,
        string riskIntro,
        string riskNone,
        string riskSubTranspiler,
        string riskSubSamePri,
        string riskSubHeavy,
        string riskHintFooter,
        string secOwners,
        string secBusiest,
        string secMulti,
        string noneMulti,
        string disclaimer,
        string colHooks,
        string colPx,
        string colPo,
        string colTr,
        string colFi,
        string colOwners,
        HarmonyPatchRegistry? patchRegistry = null,
        string? secPatchDocs = null,
        string? patchDocsIntro = null,
        string? patchDocsMissing = null) {
        var sb = new StringBuilder();
        const int W = 62;      // section header total width
        const int Sep = 2;     // column gap
        const int CHk = 5;     // "hooks" column width
        const int CSub = 4;    // px/po/tr/fi column width

        // ═══ HEADER ═══════════════════════════════════════════════
        sb.AppendLine(new string('═', W));
        sb.AppendLine($"  {title}");
        sb.AppendLine($"  {r.PatchedMethodCount} patched methods  |  {r.TotalPatchOperations} ops  |  {r.DistinctOwnerCount} owners");
        sb.AppendLine(new string('═', W));
        sb.AppendLine();

        // ── RISK FLAGS ─────────────────────────────────────────────
        AppendSectionHeader(sb, secRisk, W);
        sb.AppendLine($"  {riskIntro}");
        sb.AppendLine();

        // A) Transpiler stacks
        if (r.TranspilerStackRisks.Count == 0) {
            sb.AppendLine($"  ✓  {riskSubTranspiler}");
            sb.AppendLine();
        }
        else {
            sb.AppendLine($"  ⚠  {riskSubTranspiler}");
            foreach (var row in r.TranspilerStackRisks) {
                sb.AppendLine($"       {ShortType(row.DeclaringType)}.{row.MethodSignature}  (×{row.TranspilerCount})");
                sb.AppendLine($"       └ {row.DeclaringType}");
                foreach (var line in row.PatchLines)
                    sb.AppendLine($"         • {line}");
                sb.AppendLine();
            }
        }

        // B) Same-priority multi-owner
        if (r.SamePriorityRisks.Count == 0) {
            sb.AppendLine($"  ✓  {riskSubSamePri}");
            sb.AppendLine();
        }
        else {
            sb.AppendLine($"  ⚠  {riskSubSamePri}");
            foreach (var row in r.SamePriorityRisks) {
                sb.AppendLine($"       {row.HookKind}  pri={row.Priority}  {ShortType(row.DeclaringType)}.{row.MethodSignature}");
                sb.AppendLine($"       └ {row.DeclaringType}");
                foreach (var line in row.PatchLines)
                    sb.AppendLine($"         • {line}");
                sb.AppendLine();
            }
        }

        // C) Heavy Prefix/Postfix stacks
        if (r.HeavyHookRisks.Count == 0) {
            sb.AppendLine($"  ✓  {riskSubHeavy}");
            sb.AppendLine();
        }
        else {
            sb.AppendLine($"  ⚠  {riskSubHeavy}");
            foreach (var row in r.HeavyHookRisks) {
                sb.AppendLine($"       {row.HookKind} ×{row.Count}  {ShortType(row.DeclaringType)}.{row.MethodSignature}");
                sb.AppendLine($"       └ {row.DeclaringType}");
                foreach (var line in row.PatchLines)
                    sb.AppendLine($"         • {line}");
                sb.AppendLine();
            }
        }

        var anyRisk = r.TranspilerStackRisks.Count > 0 || r.SamePriorityRisks.Count > 0 || r.HeavyHookRisks.Count > 0;
        if (anyRisk)
            sb.AppendLine($"  → {riskHintFooter}");
        sb.AppendLine();

        // ── OWNERS ─────────────────────────────────────────────────
        AppendSectionHeader(sb, secOwners, W);
        sb.AppendLine($"  {"count",6}  owner");
        sb.AppendLine($"  ──────  {"─────────────────────────────────────────────"}");
        foreach (var (owner, n) in r.PatchesByOwner) {
            if (patchRegistry != null && patchRegistry.Count > 0 && patchRegistry.TryGet(owner, out var ownerDoc)) {
                var cat = ownerDoc.Category ?? "?";
                sb.AppendLine($"  {n,6}  {owner}  [{cat}]");
            }
            else {
                sb.AppendLine($"  {n,6}  {owner}");
            }
        }

        sb.AppendLine();

        // ── REGISTRY DOCS ──────────────────────────────────────────
        if (patchRegistry != null && patchRegistry.Count > 0 && !string.IsNullOrEmpty(secPatchDocs)) {
            AppendSectionHeader(sb, secPatchDocs, W);
            if (!string.IsNullOrEmpty(patchDocsIntro))
                sb.AppendLine($"  {patchDocsIntro}");
            sb.AppendLine();

            var anyDoc = false;
            foreach (var (owner, n) in r.PatchesByOwner) {
                if (!patchRegistry.TryGet(owner, out var doc))
                    continue;
                anyDoc = true;
                var docTitle = doc.DisplayName ?? owner;
                sb.AppendLine($"  {docTitle}  [{doc.Category ?? "?"}]  — {n} ops");
                if (!string.IsNullOrEmpty(doc.Summary))
                    sb.AppendLine($"    {doc.Summary}");
                if (!string.IsNullOrEmpty(doc.DocUrl))
                    sb.AppendLine($"    {doc.DocUrl}");
                sb.AppendLine();
            }

            if (!anyDoc && !string.IsNullOrEmpty(patchDocsMissing))
                sb.AppendLine($"  {patchDocsMissing}");
            sb.AppendLine();
        }

        // ── BUSIEST METHODS (table) ─────────────────────────────────
        // Layout: 2(pad) + (CHk+Sep) + (CSub+Sep)*4 = 2+7+24 = 33 chars before "method" col
        AppendSectionHeader(sb, secBusiest, W);
        var bIndent = new string(' ', 2 + (CHk + Sep) + (CSub + Sep) * 4);
        sb.AppendLine($"  {colHooks,CHk}  {colPx,CSub}  {colPo,CSub}  {colTr,CSub}  {colFi,CSub}  method");
        sb.AppendLine($"  {new string('─', CHk)}  {new string('─', CSub)}  {new string('─', CSub)}  {new string('─', CSub)}  {new string('─', CSub)}  {new string('─', 36)}");
        foreach (var h in r.BusiestMethods) {
            var shortName = $"{ShortType(h.DeclaringType)}.{h.MethodSignature}";
            sb.AppendLine($"  {h.TotalHooks,CHk}  {h.Prefix,CSub}  {h.Postfix,CSub}  {h.Transpiler,CSub}  {h.Finalizer,CSub}  {shortName}");
            sb.AppendLine($"{bIndent}  └ {h.DeclaringType}");
        }

        sb.AppendLine();

        // ── MULTI-OWNER METHODS (table) ─────────────────────────────
        // Layout: 2(pad) + (COw+Sep) + (CHk+Sep) = 2+8+7 = 17 chars before "method" col
        AppendSectionHeader(sb, secMulti, W);
        if (r.MultiOwnerMethods.Count == 0) {
            sb.AppendLine($"  ({noneMulti})");
        }
        else {
            const int COw = 6;
            var mIndent = new string(' ', 2 + (COw + Sep) + (CHk + Sep));
            sb.AppendLine($"  {colOwners,COw}  {colHooks,CHk}  method");
            sb.AppendLine($"  {new string('─', COw)}  {new string('─', CHk)}  {new string('─', 36)}");
            foreach (var row in r.MultiOwnerMethods) {
                var shortName = $"{ShortType(row.DeclaringType)}.{row.MethodSignature}";
                sb.AppendLine($"  {row.DistinctOwners,COw}  {row.TotalHooks,CHk}  {shortName}");
                sb.AppendLine($"{mIndent}  └ {row.DeclaringType}");
                sb.AppendLine($"{mIndent}  → {string.Join(", ", row.OwnersSorted)}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"  {disclaimer}");
        return sb.ToString();
    }

    private static void AppendSectionHeader(StringBuilder sb, string title, int totalWidth) {
        const string Prefix = "── ";
        const string Suffix = " ";
        var dashCount = totalWidth - Prefix.Length - title.Length - Suffix.Length;
        if (dashCount < 2) dashCount = 2;
        sb.AppendLine($"{Prefix}{title}{Suffix}{new string('─', dashCount)}");
    }

    private static string ShortType(string fullName) {
        var dot = fullName.LastIndexOf('.');
        return dot >= 0 ? fullName[(dot + 1)..] : fullName;
    }
}
