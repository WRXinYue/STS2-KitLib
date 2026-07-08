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

    [Fact]
    public void Move_modifier_implements_abstractions_interface() {
        using var smoke = AssemblyDefinition.ReadAssembly(SmokeModPaths.ModDll);
        using var abstractions = AssemblyDefinition.ReadAssembly(SmokeModPaths.AbstractionsDll);

        var modifier = smoke.MainModule.Types.Single(t => t.Name == "SmokeMoveModifier");
        var iface = abstractions.MainModule.Types.Single(t => t.FullName == "KitLib.AI.Core.IAiMoveModifier");

        var impl = modifier.Interfaces.SingleOrDefault(i => i.InterfaceType.Name == iface.Name);
        Assert.NotNull(impl);
        Assert.Equal(iface.FullName, impl!.InterfaceType.FullName);

        var method = modifier.Methods.Single(m => m.Name == "ModifyScore");
        var moveParam = method.Parameters[1];
        Assert.Equal("KitLib.AI.Core.Schema.GameAction", moveParam.ParameterType.FullName);
        Assert.Equal("KitLib.Abstractions", moveParam.ParameterType.Scope.Name);
    }
}
