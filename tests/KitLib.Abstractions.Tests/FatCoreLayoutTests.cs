using Mono.Cecil;

namespace KitLib.Abstractions.Tests;

public sealed class FatCoreLayoutTests {
    static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void Loader_does_not_reference_abstractions_or_variant_loader() {
        var loader = Path.Combine(RepoRoot, "build", "KitLib", "KitLib.dll");
        if (!File.Exists(loader))
            return;

        using var asm = AssemblyDefinition.ReadAssembly(loader);
        var refs = asm.MainModule.AssemblyReferences
            .Select(r => r.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("KitLib.Abstractions", refs);
        Assert.DoesNotContain("KitLib.ModVariantLoader", refs);
        Assert.DoesNotContain("KitLib.Core", refs);
        Assert.DoesNotContain("Semver", refs);
    }

    [Fact]
    public void Content_workshop_loader_does_not_reference_variant_loader() {
        var panel = Path.Combine(RepoRoot, "build", "KitModPanel", "KitModPanel.dll");
        if (!File.Exists(panel))
            return;

        using var asm = AssemblyDefinition.ReadAssembly(panel);
        var refs = asm.MainModule.AssemblyReferences
            .Select(r => r.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("KitLib.ModVariantLoader", refs);
        Assert.DoesNotContain("KitLib.Abstractions", refs);
        Assert.DoesNotContain("KitLib.Core", refs);
    }

    [Fact]
    public void Core_does_not_embed_satellite_product_types() {
        var lib = Path.Combine(RepoRoot, "build", "KitLib", "lib");
        if (!Directory.Exists(lib))
            return;

        foreach (var dir in Directory.EnumerateDirectories(lib)) {
            var core = Path.Combine(dir, "KitLib.Core.dll");
            if (!File.Exists(core))
                continue;

            using var asm = AssemblyDefinition.ReadAssembly(core);
            var names = asm.MainModule.Types.Select(t => t.FullName).ToHashSet(StringComparer.Ordinal);
            Assert.DoesNotContain("KitLib.Abstractions.ModPanel.ModPanelHostSurface", names);
            Assert.DoesNotContain("KitModPanel.ModPanelHostSurface", names);
            Assert.DoesNotContain("KitLib.AI.Core.IAiMoveModifier", names);
            Assert.DoesNotContain("KitLib.DevPerf.DevPerfMetrics", names);
        }
    }

    [Fact]
    public void Facade_forwards_every_public_abstractions_type() {
        var abstractions = Path.Combine(
            RepoRoot, "src", "KitLib", "Abstractions", "bin", "Debug", "net9.0", "KitLib.Abstractions.dll");
        if (!File.Exists(abstractions)) {
            abstractions = Path.Combine(
                RepoRoot, "src", "KitLib", "Abstractions", "bin", "Release", "net9.0", "KitLib.Abstractions.dll");
        }

        var facade = FindFacade();
        if (!File.Exists(abstractions) || facade is null)
            return;

        using var contract = AssemblyDefinition.ReadAssembly(abstractions);
        using var forwarder = AssemblyDefinition.ReadAssembly(facade);
        var forwarded = forwarder.MainModule.ExportedTypes
            .Select(t => t.FullName.Replace('/', '.'))
            .ToHashSet(StringComparer.Ordinal);

        var missing = contract.MainModule.Types
            .Where(t => t.IsPublic && !t.IsNested)
            .Select(t => t.FullName)
            .Where(name => !string.IsNullOrEmpty(name) && name != "<Module>" && !forwarded.Contains(name!))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0, "Facade missing TypeForwardedTo for: " + string.Join(", ", missing));
    }

    static string? FindFacade() {
        var lib = Path.Combine(RepoRoot, "build", "KitLib", "lib");
        if (!Directory.Exists(lib))
            return null;
        foreach (var dir in Directory.EnumerateDirectories(lib)) {
            var facade = Path.Combine(dir, "KitLib.Abstractions.dll");
            if (File.Exists(facade))
                return facade;
        }

        return null;
    }
}
