using KitLib.Abstractions.Modding;

namespace KitLib.Abstractions.Tests;

public sealed class ModVariantManifestTests {
    [Fact]
    public void ManifestFileName_UsesLowerModId() {
        Assert.Equal("lusttravel2-variants.manifest", ModVariantLayout.ManifestFileName("LustTravel2"));
    }

    [Fact]
    public void VariantFileName_EncodesCompatTarget() {
        Assert.Equal("ExampleMod_0.107.1.dll", ModVariantLayout.VariantFileName("ExampleMod", "0.107.1"));
        Assert.Equal("lib/ExampleMod_0.107.1.dll", ModVariantLayout.VariantRelativePath("ExampleMod", "0.107.1"));
    }

    [Fact]
    public void TryParseVariantFileName_AcceptsFlatLayout() {
        Assert.True(ModVariantLayout.TryParseVariantFileName("ExampleMod", "ExampleMod_0.107.1.dll", out var target));
        Assert.Equal("0.107.1", target);
    }

    [Fact]
    public void CreateFromFlatLibDirectory_WritesSha256Entries() {
        var root = Path.Combine(Path.GetTempPath(), "kitlib-mod-variant-" + Guid.NewGuid().ToString("N"));
        var lib = Path.Combine(root, "lib");
        Directory.CreateDirectory(lib);
        var dll = Path.Combine(lib, "ExampleMod_0.107.1.dll");
        File.WriteAllText(dll, "example");

        var manifest = ModVariantManifestIO.CreateFromFlatLibDirectory(lib, "ExampleMod");

        Assert.Equal(ModVariantLayout.ManifestSchema, manifest.Schema);
        Assert.Single(manifest.Variants);
        Assert.Equal("0.107.1", manifest.Variants[0].CompatTarget);
        Assert.Equal("lib/ExampleMod_0.107.1.dll", manifest.Variants[0].File);
        Assert.False(string.IsNullOrWhiteSpace(manifest.Variants[0].Sha256));

        Directory.Delete(root, recursive: true);
    }
}
