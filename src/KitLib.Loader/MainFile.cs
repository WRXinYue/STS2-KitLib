using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib;

[ModInitializer(nameof(Initialize))]
public static class MainFile {
    public const string ModId = "KitLib";
    public const string CoreFileName = "KitLib.Core.dll";
    const string LibDirectoryName = "lib";
    const string CompatTargetMarkerName = "compat-target.txt";

    static readonly string[] HostDeps = [
        "Microsoft.Extensions.Primitives.dll",
        "Semver.dll",
        "KitLib.Abstractions.dll",
    ];

    public static void Initialize() {
        var hostAssembly = typeof(MainFile).Assembly;
        var modDir = Path.GetDirectoryName(hostAssembly.Location)
            ?? throw new InvalidOperationException("KitLib loader assembly has no Location.");

        var alc = AssemblyLoadContext.GetLoadContext(hostAssembly) ?? AssemblyLoadContext.Default;
        PreloadDependencies(alc, modDir);

        var corePath = ResolveCoreAssemblyPath(alc, modDir);
        if (!File.Exists(corePath))
            throw new FileNotFoundException($"KitLib core assembly not found: {corePath}");

        var coreAsm = alc.LoadFromAssemblyPath(Path.GetFullPath(corePath));
        AssociateCoreAssembly(alc, coreAsm);
        InvokeCoreInitializer(coreAsm);
    }

    static string ResolveCoreAssemblyPath(AssemblyLoadContext alc, string modDir) {
        var flatCore = Path.Combine(modDir, CoreFileName);
        if (File.Exists(flatCore)) {
            SetActiveVariantRoot(alc, null);
            return flatCore;
        }

        var variantRoot = TryPickVariantDirectory(modDir, TryReadHostVersion());
        if (variantRoot is null)
            return flatCore;

        SetActiveVariantRoot(alc, variantRoot);
        return Path.Combine(variantRoot, CoreFileName);
    }

    static string? TryPickVariantDirectory(string modDir, Version? hostVersion) {
        var libRoot = Path.Combine(modDir, LibDirectoryName);
        if (!Directory.Exists(libRoot))
            return null;

        var candidates = new List<(string Label, Version Version, string Path)>();
        foreach (var dir in Directory.EnumerateDirectories(libRoot)) {
            var marker = Path.Combine(dir, CompatTargetMarkerName);
            if (!File.Exists(marker))
                continue;

            var label = File.ReadAllText(marker).Trim();
            if (string.IsNullOrWhiteSpace(label))
                continue;

            var core = Path.Combine(dir, CoreFileName);
            if (!File.Exists(core))
                continue;

            if (!Version.TryParse(NormalizeVersionLabel(label), out var version))
                continue;

            candidates.Add((label, version, dir));
        }

        if (candidates.Count == 0)
            return null;

        candidates.Sort(static (a, b) => a.Version.CompareTo(b.Version));

        if (hostVersion is null)
            return candidates[^1].Path;

        var picked = candidates.Where(x => x.Version <= hostVersion).ToList();
        return picked.Count > 0 ? picked[^1].Path : candidates[^1].Path;
    }

    static string NormalizeVersionLabel(string label) {
        var core = label.Trim().TrimStart('v', 'V');
        var dash = core.IndexOf('-');
        return dash >= 0 ? core[..dash] : core;
    }

    static Version? TryReadHostVersion() {
        try {
            var label = ReleaseInfoManager.Instance.ReleaseInfo?.Version;
            if (!string.IsNullOrWhiteSpace(label) && Version.TryParse(NormalizeVersionLabel(label), out var parsed))
                return parsed;
        }
        catch {
            // ReleaseInfoManager may be unavailable during unusual load order.
        }

        var av = typeof(SerializableRun).Assembly.GetName().Version;
        if (av is { Major: > 0 } or { Minor: > 0 })
            return av;

        return null;
    }

    static void SetActiveVariantRoot(AssemblyLoadContext alc, string? variantRoot) {
        var hostPaths = FindLoaded(alc, "KitLib.Abstractions")
            ?.GetType("KitLib.Abstractions.Modding.KitLibHostPaths");
        hostPaths?.GetMethod(
                "SetActiveVariantRoot",
                BindingFlags.Public | BindingFlags.Static,
                null,
                [typeof(string)],
                null)
            ?.Invoke(null, [variantRoot]);
    }

    static void AssociateCoreAssembly(AssemblyLoadContext alc, Assembly coreAsm) {
        var association = FindLoaded(alc, "KitLib.Abstractions")
            ?.GetType("KitLib.Abstractions.Modding.ModAssemblyAssociation");
        var associate = association?.GetMethod(
            "Associate",
            BindingFlags.Public | BindingFlags.Static,
            null,
            [typeof(string), typeof(Assembly)],
            null);
        if (associate is not null) {
            associate.Invoke(null, [ModId, coreAsm]);
            return;
        }

        var modManager = typeof(ModManager);
        var associateMethod = modManager.GetMethod(
            "AssociateAssemblyWithMod",
            BindingFlags.Public | BindingFlags.Static,
            null,
            [typeof(string), typeof(Assembly)],
            null);
        if (associateMethod is not null) {
            associateMethod.Invoke(null, [ModId, coreAsm]);
            return;
        }

        TryAssociateAssemblyWithModList(ModId, coreAsm);
    }

    static bool TryAssociateAssemblyWithModList(string modId, Assembly assembly) {
        if (!TryFindMod(modId, out var mod))
            return false;

        if (!TryGetMutableAssembliesList(mod, out var assemblies))
            return false;

        if (!ContainsAssembly(assemblies, assembly))
            assemblies.Add(assembly);

        return true;
    }

    static bool TryFindMod(string modId, out Mod mod) {
        foreach (var candidate in ModManager.Mods) {
            if (!string.Equals(ReadManifestId(candidate), modId, StringComparison.Ordinal))
                continue;

            mod = candidate;
            return true;
        }

        mod = null!;
        return false;
    }

    static string? ReadManifestId(Mod mod) {
        var manifest = typeof(Mod)
            .GetField("manifest", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(mod);
        return manifest?.GetType()
            .GetField("id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(manifest) as string;
    }

    static bool TryGetMutableAssembliesList(Mod mod, out IList assemblies) {
        assemblies = null!;
        var value = typeof(Mod).GetField("assemblies",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(mod);
        if (value is not IList list)
            return false;

        assemblies = list;
        return true;
    }

    static bool ContainsAssembly(IEnumerable assemblies, Assembly assembly) =>
        assemblies.Cast<object?>().Any(item => ReferenceEquals(item, assembly));

    static void PreloadDependencies(AssemblyLoadContext alc, string modDir) {
        foreach (var fileName in HostDeps) {
            var path = Path.Combine(modDir, fileName);
            if (!File.Exists(path))
                continue;

            var simple = Path.GetFileNameWithoutExtension(fileName);
            if (FindLoaded(alc, simple) is not null)
                continue;

            alc.LoadFromAssemblyPath(Path.GetFullPath(path));
        }
    }

    static void InvokeCoreInitializer(Assembly coreAsm) {
        foreach (var type in coreAsm.GetTypes()) {
            var attr = type.GetCustomAttribute<ModInitializerAttribute>();
            if (attr is null)
                continue;

            var method = type.GetMethod(
                attr.initializerMethod,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method is null)
                throw new InvalidOperationException(
                    $"Type {type.FullName} has {nameof(ModInitializerAttribute)} but no static method {attr.initializerMethod}.");

            method.Invoke(null, null);
            return;
        }

        throw new InvalidOperationException(
            $"No type with {nameof(ModInitializerAttribute)} found in {coreAsm.FullName}.");
    }

    static Assembly? FindLoaded(AssemblyLoadContext alc, string simpleName) {
        foreach (var asm in alc.Assemblies) {
            if (string.Equals(asm.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                return asm;
        }

        return null;
    }
}
