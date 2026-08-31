using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace KitLib;

/// <summary>Applies Harmony patches from a single module assembly once per process.</summary>
public static class KitLibHarmony {
    static readonly HashSet<string> Applied = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Harmony> Instances = new(StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<Type> GloballyPatchedTypes = [];
    static readonly HashSet<MethodBase> ApiMismatchFailSoftAttached = [];
    static readonly HarmonyMethod ApiMismatchFailSoft = CreateApiMismatchFailSoft();

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

    public static void Apply(Assembly moduleAssembly, string harmonyId) =>
        Apply(moduleAssembly, harmonyId, requiredPatchTypes: null);

    /// <summary>Applies only the given patch types (skips assembly-wide discovery).</summary>
    public static void ApplyOnly(Assembly moduleAssembly, string harmonyId, params Type[] patchTypes) {
        ArgumentNullException.ThrowIfNull(moduleAssembly);
        if (string.IsNullOrWhiteSpace(harmonyId))
            throw new ArgumentException("Harmony id is required.", nameof(harmonyId));
        if (IsApplied(harmonyId))
            return;

        try {
            ApplyCore(moduleAssembly, harmonyId, patchTypes, onlyRequired: true);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib Harmony apply failed for {harmonyId}: {ex.Message}");
        }
    }

    public static void Apply(Assembly moduleAssembly, string harmonyId, params Type[]? requiredPatchTypes) {
        ArgumentNullException.ThrowIfNull(moduleAssembly);
        if (string.IsNullOrWhiteSpace(harmonyId))
            throw new ArgumentException("Harmony id is required.", nameof(harmonyId));
        if (IsApplied(harmonyId))
            return;

        try {
            ApplyCore(moduleAssembly, harmonyId, requiredPatchTypes, onlyRequired: false);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib Harmony apply failed for {harmonyId}: {ex.Message}");
        }
    }

    static void ApplyCore(
        Assembly moduleAssembly,
        string harmonyId,
        Type[]? requiredPatchTypes,
        bool onlyRequired) {
        var harmony = GetOrCreate(harmonyId);
        var appliedTypes = new List<string>();
        var skipped = new List<(string Type, string Reason)>();
        var patchedTypes = new HashSet<Type>();

        if (requiredPatchTypes is { Length: > 0 }) {
            foreach (var type in requiredPatchTypes) {
                if (type == null)
                    continue;
                TryPatchType(harmony, type, appliedTypes, skipped, patchedTypes);
            }
        }

        if (onlyRequired) {
            FinishApply(harmonyId, appliedTypes, skipped);
            return;
        }

        var patchTypes = CollectPatchTypes(moduleAssembly);
        if (patchTypes.Count == 0 && appliedTypes.Count == 0) {
            try {
                harmony.PatchAll(moduleAssembly);
                Applied.Add(harmonyId);
                AttachApiMismatchFailSoft(harmony);
                MainFile.Logger.Info($"KitLib Harmony patches applied: {harmonyId} (PatchAll).");
                return;
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"KitLib Harmony PatchAll failed for {harmonyId}: {ex.Message}");
                return;
            }
        }

        foreach (var type in patchTypes) {
            TryPatchType(harmony, type, appliedTypes, skipped, patchedTypes);
        }

        FinishApply(harmonyId, appliedTypes, skipped);
    }

    static void FinishApply(
        string harmonyId,
        List<string> appliedTypes,
        List<(string Type, string Reason)> skipped) {
        if (appliedTypes.Count == 0) {
            MainFile.Logger.Warn(
                $"KitLib Harmony applied no patches for {harmonyId} " +
                "(optional satellite DLLs may be missing).");
            return;
        }

        Applied.Add(harmonyId);
        AttachApiMismatchFailSoft(GetOrCreate(harmonyId));

        MainFile.Logger.Info(
            $"KitLib Harmony patches applied: {harmonyId} ({appliedTypes.Count} types, {skipped.Count} skipped).");
        if (skipped.Count > 0) {
            MainFile.Logger.Warn(
                $"KitLib Harmony {harmonyId}: {skipped.Count} patch type(s) skipped — search godot.log for " +
                "'Harmony skipped patch type' or module health errors.");
        }
    }

    static HarmonyMethod CreateApiMismatchFailSoft() {
        var method = typeof(KitLibHarmony).GetMethod(
            nameof(SwallowIncompatibleApiException),
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: [typeof(Exception)],
            modifiers: null)
            ?? throw new InvalidOperationException("KitLib Harmony API fail-soft method is missing.");
        return new HarmonyMethod(method) { priority = Priority.Last };
    }

    static void AttachApiMismatchFailSoft(Harmony harmony) {
        foreach (var method in harmony.GetPatchedMethods()) {
            if (method == null || !ApiMismatchFailSoftAttached.Add(method))
                continue;
            try {
                harmony.Patch(method, finalizer: ApiMismatchFailSoft);
            }
            catch (Exception ex) {
                ApiMismatchFailSoftAttached.Remove(method);
                MainFile.Logger.Debug(
                    $"KitLib Harmony could not attach API fail-soft to {method.DeclaringType?.Name}.{method.Name}: {ex.Message}");
            }
        }
    }

    // Harmony postfix/prefix exceptions abort the original game method, which takes
    // down vanilla and other mods sharing that call. Version-mismatch JIT failures
    // are swallowed so the original result stands; the KitLib feature no-ops.
    static Exception? SwallowIncompatibleApiException(Exception? __exception) {
        if (__exception is null || !IsIncompatibleApiException(__exception))
            return __exception;

        var inner = UnwrapIncompatibleApiException(__exception);
        MainFile.Logger.Warn(
            $"KitLib Harmony ignored {inner.GetType().Name} on a patched game method ({inner.Message}). " +
            "Vanilla continues; this KitLib feature is disabled for this call.");
        return null;
    }

    static bool IsIncompatibleApiException(Exception? exception) {
        for (var ex = exception; ex != null; ex = ex.InnerException) {
            if (ex is MissingMethodException or MissingFieldException or TypeLoadException)
                return true;
        }

        return false;
    }

    static Exception UnwrapIncompatibleApiException(Exception exception) {
        for (var ex = exception; ex != null; ex = ex.InnerException) {
            if (ex is MissingMethodException or MissingFieldException or TypeLoadException)
                return ex;
        }

        return exception;
    }

    static List<Type> CollectPatchTypes(Assembly assembly) {
        var patchTypes = new List<Type>();
        foreach (var type in TryEnumerateAssemblyTypes(assembly)) {
            if (type == null || GloballyPatchedTypes.Contains(type))
                continue;
            try {
                if (HasHarmonyPatch(type))
                    patchTypes.Add(type);
            }
            catch (Exception ex) {
                MainFile.Logger.Debug(
                    $"KitLib Harmony skipped patch candidate {type.FullName}: {ex.Message}");
            }
        }
        return patchTypes;
    }

    static void TryPatchType(
        Harmony harmony,
        Type type,
        List<string> appliedTypes,
        List<(string Type, string Reason)> skipped,
        HashSet<Type> patchedTypes) {
        if (!patchedTypes.Add(type) || !GloballyPatchedTypes.Add(type))
            return;

        try {
            harmony.CreateClassProcessor(type).Patch();
            appliedTypes.Add(type.FullName ?? type.Name);
        }
        catch (Exception ex) {
            GloballyPatchedTypes.Remove(type);
            skipped.Add((type.FullName ?? type.Name, ex.Message));
            MainFile.Logger.Warn($"KitLib Harmony skipped patch type {type.FullName}: {ex.Message}");
        }
    }

    static IEnumerable<Type?> TryEnumerateAssemblyTypes(Assembly assembly) {
        Type?[]? types;
        try {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex) {
            LogTypeLoaderExceptions(assembly, ex);
            types = ex.Types;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn(
                $"KitLib Harmony: {assembly.GetName().Name} type enumeration failed ({ex.Message}); " +
                "using per-type fallback.");
            return EnumerateDefinedTypes(assembly);
        }

        if (types == null)
            return [];

        return types;
    }

    static IEnumerable<Type?> EnumerateDefinedTypes(Assembly assembly) {
        foreach (var typeInfo in assembly.DefinedTypes) {
            Type? type = null;
            try {
                type = typeInfo.AsType();
            }
            catch (Exception typeEx) {
                MainFile.Logger.Debug(
                    $"KitLib Harmony skipped type {typeInfo.FullName}: {typeEx.Message}");
            }
            if (type != null)
                yield return type;
        }
    }

    static void LogTypeLoaderExceptions(Assembly assembly, ReflectionTypeLoadException ex) {
        var messages = ex.LoaderExceptions?
            .Where(e => e != null)
            .Select(e => e!.Message)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (messages is not { Count: > 0 })
            return;

        var preview = string.Join("; ", messages.Take(3));
        if (messages.Count > 3)
            preview += "...";
        MainFile.Logger.Warn(
            $"KitLib Harmony: {assembly.GetName().Name} skipped {messages.Count} unloaded type(s) ({preview}).");
    }

    static bool HasHarmonyPatch(Type type) {
        try {
            foreach (var attr in type.GetCustomAttributes(inherit: false)) {
                var attrType = attr.GetType();
                if (attrType == typeof(HarmonyPatch) || attrType.Name is "HarmonyPatch" or "HarmonyPatchAttribute")
                    return true;
            }
        }
        catch (Exception ex) {
            MainFile.Logger.Debug($"KitLib Harmony could not read attributes on {type.FullName}: {ex.Message}");
        }

        return false;
    }
}
