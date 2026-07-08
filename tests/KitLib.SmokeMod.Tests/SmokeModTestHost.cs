using System.Reflection;
using System.Runtime.Loader;

namespace KitLib.SmokeMod.Tests;

internal static class SmokeModTestHost {
    static readonly AssemblyLoadContext Alc = new("kitlib-smoke-mod-test", isCollectible: false);

    static SmokeModTestHost() => Alc.Resolving += (_, name) => Resolve(name.Name);

    public static void InvokeModInitializer() {
        var sts2 = Alc.LoadFromAssemblyPath(Path.Combine(SmokeModPaths.Sts2DataDir, "sts2.dll"));
        _ = Alc.LoadFromAssemblyPath(SmokeModPaths.AbstractionsDll);
        _ = Alc.LoadFromAssemblyPath(SmokeModPaths.KitLibDll);

        var smoke = Alc.LoadFromAssemblyPath(SmokeModPaths.ModDll);
        var initializerAttr = sts2.GetType("MegaCrit.Sts2.Core.Modding.ModInitializerAttribute", throwOnError: true)!;

        foreach (var type in smoke.GetTypes()) {
            var attr = type.GetCustomAttribute(initializerAttr);
            if (attr is null)
                continue;

            var methodName = (string)initializerAttr.GetField("initializerMethod")!.GetValue(attr)!;
            var method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Initializer {methodName} not found on {type.FullName}.");

            method.Invoke(null, null);
            return;
        }

        throw new InvalidOperationException("No ModInitializer found in smoke mod assembly.");
    }

    static Assembly? Resolve(string? simpleName) {
        if (string.IsNullOrWhiteSpace(simpleName))
            return null;

        if (string.Equals(simpleName, "KitLib.Abstractions", StringComparison.OrdinalIgnoreCase)
            && File.Exists(SmokeModPaths.AbstractionsDll))
            return Alc.LoadFromAssemblyPath(SmokeModPaths.AbstractionsDll);

        if ((string.Equals(simpleName, "KitLib", StringComparison.OrdinalIgnoreCase)
                || string.Equals(simpleName, "KitLib.Core", StringComparison.OrdinalIgnoreCase))
            && File.Exists(SmokeModPaths.KitLibDll))
            return Alc.LoadFromAssemblyPath(SmokeModPaths.KitLibDll);

        var sts2Candidate = Path.Combine(SmokeModPaths.Sts2DataDir, $"{simpleName}.dll");
        if (File.Exists(sts2Candidate))
            return Alc.LoadFromAssemblyPath(sts2Candidate);

        var kitLibDir = Path.Combine(SmokeModPaths.RepoRoot, "build", "KitLib", $"{simpleName}.dll");
        if (File.Exists(kitLibDir))
            return Alc.LoadFromAssemblyPath(kitLibDir);

        return null;
    }
}
