using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.Modding;

internal static class ModAssemblyLookup {
    private static readonly Lazy<Dictionary<string, KitLibModInfo>> _byAssemblySimpleName =
        new(BuildAssemblyMap, isThreadSafe: true);
    private static readonly HashSet<string> _nonModAssemblies = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<Type, string?> _modDirectoryCache  = new();

    internal static bool TryGetByAssemblySimpleName(string? assemblySimpleName, out KitLibModInfo info) {
        info = default;
        if (string.IsNullOrEmpty(assemblySimpleName))
            return false;

        // EnsureBuilt();
        if (_nonModAssemblies.Contains(assemblySimpleName))
            return false;

        var map = _byAssemblySimpleName.Value;
        return map.TryGetValue(assemblySimpleName, out info);
    }

    private static Dictionary<string, KitLibModInfo> BuildAssemblyMap() {
        var map = new Dictionary<string, KitLibModInfo>(StringComparer.Ordinal);

        foreach (var mod in ModManagerLoadedMods.Enumerate()) {
            var man = mod.manifest;
            // if (string.IsNullOrEmpty(mod.manifest?.id)) {
            //     MainFile.Logger.Warn($"Skipping mod with invalid manifest: {mod.GetType().Name}");
            //     continue;
            // }
            if (man == null || string.IsNullOrEmpty(man.id))
                continue;

            var display = string.IsNullOrEmpty(man.name) ? man.id : man.name;
            var info = new KitLibModInfo(man.id, display, man.version ?? "", ModRuntime.CopyDependencies(man));
            RegisterAssembliesForMod(mod, info, map);
        }

        LogAssemblyMapStats(map);
        return map;
    }

    // private static void EnsureBuilt() {
    //     lock (InitLock) {
    //         if (_byAssemblySimpleName != null)
    //             return;

    //         var map = new Dictionary<string, KitLibModInfo>(StringComparer.Ordinal);
    //         foreach (var mod in ModManagerLoadedMods.Enumerate()) {
    //             var man = mod.manifest;
    //             if (man == null || string.IsNullOrEmpty(man.id))
    //                 continue;

    //             var display = string.IsNullOrEmpty(man.name) ? man.id : man.name;
    //             var info = new KitLibModInfo(man.id, display, man.version ?? "");
    //             RegisterAssembliesForMod(mod, info, map);
    //         }

    //         _byAssemblySimpleName = map;
    //     }
    // }

    private static void RegisterAssembliesForMod(Mod mod, KitLibModInfo info,  Dictionary<string, KitLibModInfo> map) {
        var modDir = GetOrCacheModDirectory(mod);

        foreach (var asm in EnumerateAssembliesLinkedToMod(mod, modDir)) {
            var simple = asm.GetName().Name;
            if (string.IsNullOrEmpty(simple))
                continue;
            if (!map.ContainsKey(simple))
                map[simple] = info;
        }
    }

    private static IEnumerable<Assembly> EnumerateAssembliesLinkedToMod(Mod mod, string? modDirectory) {
        var seen = new HashSet<Assembly>();

        foreach (var asm in ExtractAssembliesViaReflection(mod)) {
            if (seen.Add(asm))
                yield return asm;
        }

        // var dir = TryGetExistingDirectoryFromMod(mod);
        if (string.IsNullOrEmpty(modDirectory))
            yield break;

        // string fullDir;
        // try {
        //     fullDir = Path.GetFullPath(dir);
        // }
        // catch {
        //     yield break;
        // }

        if (!TryGetFullPath(modDirectory, out var fullDir))
            yield break;

        // Ensure the directory ends with a path separator to avoid prefix matching errors
        // For example: C:\Mods\MyMod should not match C:\Mods\MyModOther
        var normalizedDir = fullDir.EndsWith(Path.DirectorySeparatorChar.ToString())
            ? fullDir
            : fullDir + Path.DirectorySeparatorChar;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
            if (!TryGetFullPath(asm.GetAssemblyLocation(), out var fullLoc))
                continue;

            if (!fullLoc.StartsWith(normalizedDir, StringComparison.OrdinalIgnoreCase))
                continue;

            if (seen.Add(asm))
                yield return asm;
        }

        // foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
        //     string loc;
        //     try {
        //         loc = asm.Location;
        //     }
        //     catch {
        //         continue;
        //     }

        //     if (string.IsNullOrEmpty(loc))
        //         continue;

        //     string fullLoc;
        //     try {
        //         fullLoc = Path.GetFullPath(loc);
        //     }
        //     catch {
        //         continue;
        //     }

        //     if (!fullLoc.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase))
        //         continue;

        //     if (seen.Add(asm))
        //         yield return asm;
        // }
    }

    private static IEnumerable<Assembly> ExtractAssembliesViaReflection(Mod mod) {
        const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var modType = mod.GetType();

        foreach (var member in modType.GetMembers(bf)) {
            object? value = null;

            try {
                value = member switch {
                    PropertyInfo prop when prop.GetIndexParameters().Length == 0
                        && IsAssemblyType(prop.PropertyType)
                        => prop.GetValue(mod),
                    FieldInfo field when IsAssemblyType(field.FieldType)
                        => field.GetValue(mod),
                    _ => null
                };
            }
            catch (Exception ex) {
                LogReflectionError(modType, member.Name, ex);
                continue;
            }

            if (value == null) continue;

            foreach (var asm in UnpackAssemblyValue(value)) {
                yield return asm;
            }
        }

        // foreach (var prop in modType.GetProperties(bf)) {
        //     if (prop.GetIndexParameters().Length != 0)
        //         continue;
        //     object? val;
        //     try {
        //         val = prop.GetValue(mod);
        //     }
        //     catch {
        //         continue;
        //     }

        //     foreach (var asm in UnpackAssemblyValue(val))
        //         yield return asm;
        // }

        // foreach (var field in t.GetFields(bf)) {
        //     object? val;
        //     try {
        //         val = field.GetValue(mod);
        //     }
        //     catch {
        //         continue;
        //     }

        //     foreach (var asm in UnpackAssemblyValue(val))
        //         yield return asm;
        // }
    }

    private static bool IsAssemblyType(Type type) {
        return type == typeof(Assembly) || type == typeof(Assembly[]) || typeof(IEnumerable<Assembly>).IsAssignableFrom(type);
    }

    private static IEnumerable<Assembly> UnpackAssemblyValue(object? val) {
        switch (val) {
            case Assembly a:
                yield return a;
                yield break;
            case Assembly[] arr:
                foreach (var x in arr)
                    if (x != null)
                        yield return x;
                yield break;
            case IEnumerable<Assembly> e:
                foreach (var x in e)
                    if (x != null)
                        yield return x;
                yield break;
        }

        if (val is IEnumerable legacy && val is not string) {
            foreach (var item in legacy)
                if (item is Assembly a)
                    yield return a;
        }
    }

    private static string? GetOrCacheModDirectory(Mod mod) {
        var modType = mod.GetType();
        return _modDirectoryCache.GetOrAdd(modType, type => TryFindModDirectory(mod, type));
    }


    private static string? TryFindModDirectory(Mod mod, Type modType) {
        const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        foreach (var prop in modType.GetProperties(bf)) {
            if (prop.PropertyType != typeof(string) || prop.GetIndexParameters().Length != 0)
                continue;

            string? path;
            try {
                path = prop.GetValue(mod) as string;
            }
            catch {
                continue;
            }

            if (string.IsNullOrEmpty(path))
                continue;

            if (Directory.Exists(path))
                return path;

            var parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                return parent;
        }

        return null;
    }

    // private static string? TryGetExistingDirectoryFromMod(Mod mod) {
    //     const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    //     foreach (var prop in mod.GetType().GetProperties(bf)) {
    //         if (prop.PropertyType != typeof(string) || prop.GetIndexParameters().Length != 0)
    //             continue;
    //         string? s;
    //         try {
    //             s = prop.GetValue(mod) as string;
    //         }
    //         catch {
    //             continue;
    //         }

    //         if (string.IsNullOrEmpty(s))
    //             continue;

    //         try {
    //             if (Directory.Exists(s))
    //                 return s;
    //             var parent = Path.GetDirectoryName(s);
    //             if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
    //                 return parent;
    //         }
    //         catch {
    //             // ignore
    //         }
    //     }

    //     return null;
    // }

    private static string GetAssemblyLocation(this Assembly assembly) {
        try {
            return assembly.IsDynamic ? string.Empty : assembly.Location;
        }
        catch {
            return string.Empty;
        }
    }

    private static bool TryGetFullPath(string? path, out string fullPath) {
        fullPath = string.Empty;
        if (string.IsNullOrEmpty(path)) return false;

        try {
            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch {
            return false;
        }
    }

    internal static bool IsRuntimeInfrastructureAssembly(string assemblySimpleName) {
        if (string.IsNullOrEmpty(assemblySimpleName))
            return true;

        if (assemblySimpleName.StartsWith("System.", StringComparison.Ordinal))
            return true;

        // return assemblySimpleName is "0Harmony"
        //     or "mscorlib"
        //     or "netstandard"
        //     or "GodotSharp"
        //     or "GodotPlugins"
        //     || assemblySimpleName.StartsWith("MonoMod.", StringComparison.Ordinal);


        bool isInfra = assemblySimpleName switch {
            "0Harmony" or "mscorlib" or "netstandard" or
            "GodotSharp" or "GodotPlugins" => true,
            _ => assemblySimpleName.StartsWith("MonoMod.", StringComparison.Ordinal)
        };

        if (isInfra) {
            _nonModAssemblies.Add(assemblySimpleName);
        }

        return isInfra;
    }

    internal static bool IsGameCoreAssembly(string assemblySimpleName) =>
        string.Equals(assemblySimpleName, "sts2", StringComparison.Ordinal);

    internal static string FormatAssemblyVersion(Assembly asm) {
        var an = asm.GetName();
        var ver = an.Version?.ToString() ?? "?";
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(info) && !string.Equals(info, ver, StringComparison.Ordinal))
            return $"{ver} (informational: {info})";
        return ver;
    }

    #region Logging Helpers

    private static void LogAssemblyMapStats(Dictionary<string, KitLibModInfo> map) {
        MainFile.Logger.Info(
            $"[ModAssemblyLookup] Built assembly map with {map.Count} entries");
    }

    private static void LogReflectionError(Type modType, string memberName, Exception ex) {
        MainFile.Logger.Warn(
            $"[ModAssemblyLookup] Failed to reflect member '{memberName}' on '{modType.Name}': {ex.Message}");
    }

    #endregion
}
