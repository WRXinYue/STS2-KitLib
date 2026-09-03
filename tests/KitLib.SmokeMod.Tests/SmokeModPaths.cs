namespace KitLib.SmokeMod.Tests;

internal static class SmokeModPaths {
    const string StableCompatTarget = "0.107.1";

    public static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    public static string ModDir => Path.Combine(RepoRoot, "build", "samples", "KitLibSmokeMod");

    public static string ModDll =>
        Environment.GetEnvironmentVariable("KITLIB_SMOKE_MOD_DLL")
        ?? Path.Combine(ModDir, "KitLibSmokeMod.dll");

    public static string Manifest => Path.Combine(ModDir, "mod_manifest.json");

    public static string KitLibDll {
        get {
            if (Environment.GetEnvironmentVariable("KITLIB_DLL") is { Length: > 0 } fromEnv)
                return Path.GetFullPath(fromEnv);

            var root = Path.Combine(RepoRoot, "build", "KitLib");
            var lib = Path.Combine(root, "lib");
            if (Directory.Exists(lib)) {
                foreach (var dir in Directory.EnumerateDirectories(lib)) {
                    var core = Path.Combine(dir, "KitLib.Core.dll");
                    if (File.Exists(core))
                        return core;
                }
            }

            return Path.Combine(root, "lib", StableCompatTarget, "KitLib.Core.dll");
        }
    }

    public static string AbstractionsDll {
        get {
            var bundle = Path.Combine(RepoRoot, "build", "KitLib", "KitLib.Abstractions.dll");
            if (File.Exists(bundle))
                return bundle;

            return Path.Combine(RepoRoot, "src", "KitLib.Abstractions", "bin", "Debug", "net9.0", "KitLib.Abstractions.dll");
        }
    }

    public static string Sts2DataDir {
        get {
            var fromEnv = Environment.GetEnvironmentVariable("KITLIB_STS2_DATA_DIR");
            if (!string.IsNullOrWhiteSpace(fromEnv))
                return Path.GetFullPath(fromEnv);

            return Path.Combine(
                RepoRoot,
                "eng",
                "sts2-refs",
                "stable",
                StableCompatTarget,
                "data_sts2_windows_x86_64");
        }
    }

    public static bool RuntimeLoadSupported =>
        File.Exists(ModDll)
        && File.Exists(Manifest)
        && File.Exists(KitLibDll)
        && File.Exists(AbstractionsDll)
        && File.Exists(Path.Combine(Sts2DataDir, "sts2.dll"));
}
