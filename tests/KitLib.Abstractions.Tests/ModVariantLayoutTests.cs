using KitLib.Abstractions.Modding;

namespace KitLib.Abstractions.Tests;

[CollectionDefinition("KitLibHostPaths", DisableParallelization = true)]
public sealed class KitLibHostPathsCollection;

[Collection("KitLibHostPaths")]
public sealed class ModVariantLayoutTests {
    [Fact]
    public void VariantRelativeImplementationPath_UsesApiDirectory() {
        Assert.Equal("lib/0.107.1/ExampleMod.dll", ModVariantLayout.VariantRelativeImplementationPath("ExampleMod", "0.107.1"));
    }

    [Fact]
    public void ResolveBundledAssemblyPath_PicksNewestNotAboveHost() {
        var root = Path.Combine(Path.GetTempPath(), "kitlib-mod-variant-" + Guid.NewGuid().ToString("N"));
        try {
            WriteVariant(root, "0.107.1");
            WriteVariant(root, "0.110.1");

            var picked = ModVariantAssemblyPaths.ResolveBundledAssemblyPath(root, "ExampleMod", new Version(0, 107, 1));
            Assert.NotNull(picked);
            Assert.Equal("0.107.1", Path.GetFileName(Path.GetDirectoryName(picked)));
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryPickVariantDirectory_RequiresImplementationFile() {
        var root = Path.Combine(Path.GetTempPath(), "kitlib-mod-variant-" + Guid.NewGuid().ToString("N"));
        try {
            WriteVariant(root, "0.107.1");
            var picked = KitLibHostPaths.TryPickVariantDirectory(root, new Version(0, 107, 1), "ExampleMod.dll");
            Assert.NotNull(picked);
            Assert.Null(KitLibHostPaths.TryPickVariantDirectory(root, new Version(0, 107, 1)));
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryResolveSatelliteAssemblyPath_PrefersVariantMatchingActiveKitLib() {
        var mods = Path.Combine(Path.GetTempPath(), "kitlib-sat-search-" + Guid.NewGuid().ToString("N"));
        var kitLib = Path.Combine(mods, "KitLib");
        var previous = KitLibHostPaths.ActiveVariantRoot;
        try {
            WriteProductVariant(kitLib, "0.107.1", "KitLib.Core.dll");
            WriteProductVariant(kitLib, "0.110.1", "KitLib.Core.dll");
            WriteSatelliteVariant(kitLib, "0.107.1", "KitLib.Dev.dll", "stable-dev");
            WriteSatelliteVariant(kitLib, "0.110.1", "KitLib.Dev.dll", "beta-dev");

            var stableRoot = Path.Combine(kitLib, ModVariantLayout.LibDirectoryName, "0.107.1");
            KitLibHostPaths.SetActiveVariantRoot(stableRoot);

            var picked = KitLibHostPaths.TryResolveSatelliteAssemblyPath(stableRoot, "KitLib.Dev");
            Assert.NotNull(picked);
            Assert.Equal("0.107.1", Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(picked))));
            Assert.Equal("stable-dev", File.ReadAllText(picked));
        }
        finally {
            KitLibHostPaths.SetActiveVariantRoot(previous);
            Directory.Delete(mods, recursive: true);
        }
    }

    [Fact]
    public void TryResolveSatelliteAssemblyPath_finds_modules_inside_workshop_numeric_KitLib() {
        var root = Path.Combine(Path.GetTempPath(), "kitlib-ws-sat-" + Guid.NewGuid().ToString("N"));
        var workshopKitLib = Path.Combine(root, "2868840", "3747619669");
        var previous = KitLibHostPaths.ActiveVariantRoot;
        try {
            WriteProductVariant(workshopKitLib, "0.110.1", "KitLib.Core.dll");
            WriteSatelliteVariant(workshopKitLib, "0.110.1", "KitLib.Dev.dll", "workshop-dev");

            var variantRoot = Path.Combine(workshopKitLib, ModVariantLayout.LibDirectoryName, "0.110.1");
            KitLibHostPaths.SetActiveVariantRoot(variantRoot);

            var picked = KitLibHostPaths.TryResolveSatelliteAssemblyPath(variantRoot, "KitLib.Dev");
            Assert.NotNull(picked);
            Assert.Equal("workshop-dev", File.ReadAllText(picked));
        }
        finally {
            KitLibHostPaths.SetActiveVariantRoot(previous);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryResolveSatelliteAssemblyPath_DoesNotLoadMismatchedVariant() {
        var mods = Path.Combine(Path.GetTempPath(), "kitlib-sat-skip-" + Guid.NewGuid().ToString("N"));
        var kitLib = Path.Combine(mods, "KitLib");
        var previous = KitLibHostPaths.ActiveVariantRoot;
        try {
            WriteProductVariant(kitLib, "0.107.1", "KitLib.Core.dll");
            WriteSatelliteVariant(kitLib, "0.110.1", "KitLib.Dev.dll", "beta-only");

            var stableRoot = Path.Combine(kitLib, ModVariantLayout.LibDirectoryName, "0.107.1");
            KitLibHostPaths.SetActiveVariantRoot(stableRoot);

            Assert.Null(KitLibHostPaths.TryResolveSatelliteAssemblyPath(stableRoot, "KitLib.Dev"));
        }
        finally {
            KitLibHostPaths.SetActiveVariantRoot(previous);
            Directory.Delete(mods, recursive: true);
        }
    }

    [Fact]
    public void TryResolveCoreAssemblyPath_UsesVariantAndNeverKitLibLoader() {
        var kitLib = Path.Combine(Path.GetTempPath(), "kitlib-core-path-" + Guid.NewGuid().ToString("N"));
        var previous = KitLibHostPaths.ActiveVariantRoot;
        try {
            Directory.CreateDirectory(kitLib);
            File.WriteAllText(Path.Combine(kitLib, "KitLib.dll"), "loader");
            WriteProductVariant(kitLib, "0.107.1", "KitLib.Core.dll");

            KitLibHostPaths.SetActiveVariantRoot(null);
            var picked = KitLibHostPaths.TryResolveCoreAssemblyPath(kitLib, new Version(0, 107, 1));
            Assert.NotNull(picked);
            Assert.Equal("KitLib.Core.dll", Path.GetFileName(picked));
            Assert.Equal("0.107.1", Path.GetFileName(Path.GetDirectoryName(picked)));
            Assert.EndsWith(Path.Combine("0.107.1", "KitLib.Core.dll"), picked, StringComparison.OrdinalIgnoreCase);
        }
        finally {
            KitLibHostPaths.SetActiveVariantRoot(previous);
            Directory.Delete(kitLib, recursive: true);
        }
    }

    [Fact]
    public void ResolveModFolder_WalksUpFromVariantAndModules() {
        var mods = Path.Combine(Path.GetTempPath(), "kitlib-mod-folder-" + Guid.NewGuid().ToString("N"));
        var kitLib = Path.Combine(mods, "KitLib");
        try {
            WriteVariant(kitLib, "0.110.1");
            var variant = Path.Combine(kitLib, ModVariantLayout.LibDirectoryName, "0.110.1");
            var modules = Path.Combine(variant, KitLibHostPaths.ModulesSubdir);
            Directory.CreateDirectory(modules);

            Assert.Equal(Path.GetFullPath(kitLib), KitLibHostPaths.ResolveModFolder(variant));
            Assert.Equal(Path.GetFullPath(kitLib), KitLibHostPaths.ResolveModFolder(modules));
            Assert.Equal(Path.GetFullPath(mods), KitLibHostPaths.ResolveModsRoot(variant));
            Assert.Equal(Path.GetFullPath(kitLib), KitLibHostPaths.ResolveSiblingKitLibModDirectory(variant));
            Assert.Equal(Path.GetFullPath(kitLib), KitLibHostPaths.ResolveSiblingKitLibModDirectory(modules));
        }
        finally {
            Directory.Delete(mods, recursive: true);
        }
    }

    static void WriteVariant(string modRoot, string compat) {
        var dir = Path.Combine(modRoot, ModVariantLayout.LibDirectoryName, compat);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ModVariantLayout.CompatTargetMarkerName), compat + "\n");
        File.WriteAllText(Path.Combine(dir, "ExampleMod.dll"), "example");
    }

    static void WriteProductVariant(string modRoot, string compat, string fileName) {
        var dir = Path.Combine(modRoot, ModVariantLayout.LibDirectoryName, compat);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ModVariantLayout.CompatTargetMarkerName), compat + "\n");
        File.WriteAllText(Path.Combine(dir, fileName), "core");
    }

    static void WriteSatelliteVariant(string productRoot, string compat, string fileName, string contents) {
        var modules = Path.Combine(productRoot, ModVariantLayout.LibDirectoryName, compat, KitLibHostPaths.ModulesSubdir);
        Directory.CreateDirectory(modules);
        File.WriteAllText(
            Path.Combine(productRoot, ModVariantLayout.LibDirectoryName, compat, ModVariantLayout.CompatTargetMarkerName),
            compat + "\n");
        File.WriteAllText(Path.Combine(modules, fileName), contents);
    }
}
