using Mono.Cecil;

namespace KitLib.SmokeMod.Tests;

public sealed class SmokeModLoadTests {
    [Fact]
    public void MainFile_has_mod_initializer_attribute() {
        using var smoke = AssemblyDefinition.ReadAssembly(SmokeModPaths.ModDll);
        var main = smoke.MainModule.Types.Single(t => t.Name == "MainFile");
        var hasInitializer = main.CustomAttributes.Any(
            a => a.AttributeType.FullName == "MegaCrit.Sts2.Core.Modding.ModInitializerAttribute");

        Assert.True(hasInitializer);
    }

    [Fact]
    public void Mod_initializer_runs_without_throwing_when_kitlib_is_present() {
        Assert.True(
            SmokeModPaths.RuntimeLoadSupported,
            "Smoke mod runtime load requires make build + check-smoke-mod outputs.");

        var ex = Record.Exception(SmokeModTestHost.InvokeModInitializer);
        Assert.Null(ex);
    }
}
