using KitLib.Abstractions.Modding;

namespace KitLib.Abstractions.Tests;

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

            var kitAi = Path.Combine(mods, "KitAI");
            WriteVariant(kitAi, "0.110.1");
            var aiVariant = Path.Combine(kitAi, ModVariantLayout.LibDirectoryName, "0.110.1");
            Assert.Equal(Path.GetFullPath(kitLib), KitLibHostPaths.ResolveSiblingKitLibModDirectory(aiVariant));
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
}
