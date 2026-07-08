using System.Text.Json;

namespace KitLib.SmokeMod.Tests;

public sealed class SmokeModPackageTests {
    [Fact]
    public void Mod_folder_has_dll_and_manifest() {
        Assert.True(File.Exists(SmokeModPaths.ModDll), $"Missing {SmokeModPaths.ModDll}. Run make check-smoke-mod.");
        Assert.True(File.Exists(SmokeModPaths.Manifest), $"Missing {SmokeModPaths.Manifest}.");
    }

    [Fact]
    public void Manifest_matches_loader_expectations() {
        using var doc = JsonDocument.Parse(File.ReadAllText(SmokeModPaths.Manifest));

        Assert.Equal("KitLibSmokeMod", doc.RootElement.GetProperty("id").GetString());
        Assert.True(doc.RootElement.GetProperty("has_dll").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("has_pck").GetBoolean());

        var deps = doc.RootElement.GetProperty("dependencies");
        Assert.Equal(JsonValueKind.Array, deps.ValueKind);
        Assert.Contains(
            deps.EnumerateArray(),
            dep => dep.GetProperty("id").GetString() == "KitLib");
    }
}
