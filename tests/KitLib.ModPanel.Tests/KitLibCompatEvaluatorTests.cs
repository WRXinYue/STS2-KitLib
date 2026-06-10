using KitLib.Abstractions.Modding;

namespace KitLib.ModPanel.Tests;

public sealed class KitLibCompatEvaluatorTests {
    static KitLibCompatRuntime Runtime(
        string? game = "0.106.1",
        string? kitLib = "0.13.0",
        params string[] loadedModules) {
        var loaded = new HashSet<string>(loadedModules, StringComparer.OrdinalIgnoreCase);
        return new KitLibCompatRuntime {
            GameVersion = game,
            KitLibVersion = kitLib,
            IsModuleLoaded = loaded.Contains,
        };
    }

    [Fact]
    public void Evaluate_no_constraints_is_compatible() {
        var result = KitLibCompatEvaluator.Evaluate(new KitLibCompatDocument(), Runtime());
        Assert.True(result.IsCompatible);
    }

    [Fact]
    public void Evaluate_game_version_array_or_semantics() {
        var doc = new KitLibCompatDocument {
            GameVersionRanges = [">=0.106.0 <0.107.0", "^0.105.0"],
        };
        Assert.True(KitLibCompatEvaluator.Evaluate(doc, Runtime(game: "0.106.2")).IsCompatible);
        Assert.True(KitLibCompatEvaluator.Evaluate(doc, Runtime(game: "0.105.9")).IsCompatible);
        Assert.False(KitLibCompatEvaluator.Evaluate(doc, Runtime(game: "0.107.0")).IsCompatible);
    }

    [Fact]
    public void Evaluate_kitlib_version_single_range() {
        var doc = new KitLibCompatDocument {
            KitLibVersionRanges = [">=0.13.0 <0.14.0"],
        };
        Assert.True(KitLibCompatEvaluator.Evaluate(doc, Runtime(kitLib: "0.13.5")).IsCompatible);
        Assert.False(KitLibCompatEvaluator.Evaluate(doc, Runtime(kitLib: "0.12.9")).IsCompatible);
    }

    [Fact]
    public void Evaluate_modules_requires_loaded_ids() {
        var doc = new KitLibCompatDocument {
            KitLibModules = ["KitLib", "KitLib.Dev"],
        };
        var ok = KitLibCompatEvaluator.Evaluate(
            doc,
            Runtime(loadedModules: ["KitLib", "KitLib.Panel", "KitLib.Dev"]));
        Assert.True(ok.IsCompatible);

        var missing = KitLibCompatEvaluator.Evaluate(
            doc,
            Runtime(loadedModules: ["KitLib", "KitLib.Panel"]));
        Assert.False(missing.IsCompatible);
        Assert.Contains("KitLib.Dev", missing.MissingModules);
    }

    [Fact]
    public void Evaluate_game_version_strips_v_prefix() {
        var doc = new KitLibCompatDocument {
            GameVersionRanges = [">=0.106.0 <0.107.0"],
        };
        Assert.True(KitLibCompatEvaluator.Evaluate(doc, Runtime(game: "v0.106.5")).IsCompatible);
    }
}
