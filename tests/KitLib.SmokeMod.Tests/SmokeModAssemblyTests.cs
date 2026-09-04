using Mono.Cecil;

namespace KitLib.SmokeMod.Tests;

public sealed class SmokeModAssemblyTests {
    [Fact]
    public void References_abstractions_not_kitlib_ai() {
        using var asm = AssemblyDefinition.ReadAssembly(SmokeModPaths.ModDll);
        var refs = asm.MainModule.AssemblyReferences
            .Select(r => r.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("KitLib.Abstractions", refs);
        Assert.DoesNotContain("KitLib.AI", refs);
    }
}
