using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitLib.DevTools.Tests;

/// <summary>
/// <para xml:lang="en">Guards the rail icon bundle: the Hud assembly draws every tab icon registered through
/// <c>KitLibHost.RegisterTab</c>, so each icon key declared by a <c>*TabRegistration.cs</c> must be tree-shaken
/// into Hud's <c>mdi-used.json</c>. When a satellite moves and <c>scripts/shake_icons.py</c> is not updated,
/// the icon silently disappears from the bundle and the rail logs "icon not found" on every repaint.</para>
/// <para xml:lang="zh-CN">守护栏位图标包：Hud 负责绘制所有注册到 <c>KitLibHost.RegisterTab</c> 的页签图标，
/// 因此每个 <c>*TabRegistration.cs</c> 声明的图标键都必须被收入 Hud 的 <c>mdi-used.json</c>。
/// 若模块迁移后忘记更新 <c>scripts/shake_icons.py</c>，图标会从包中消失，栏位每次重绘都会刷 "icon not found"。</para>
/// </summary>
public sealed class RailTabIconBundleTests {
    static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    static readonly Regex IconKeyAssignment = new(@"IconKey\s*=\s*""([^""]+)""", RegexOptions.Compiled);

    static readonly Regex TabRegistrationIconArgument =
        new(@"Register(?:Action)?Tab\(\s*""[^""]+""\s*,\s*""([^""]+)""", RegexOptions.Compiled);

    [Fact]
    public void Every_registered_rail_tab_icon_is_bundled_with_the_hud() {
        var src = Path.Combine(RepoRoot, "src");
        var bundlePath = Path.Combine(src, "KitLib", "Modules", "Hud", "src", "Icons", "mdi-used.json");
        Assert.True(Directory.Exists(src), $"Repository sources not found from {RepoRoot}");
        Assert.True(File.Exists(bundlePath), $"Hud icon bundle missing: {bundlePath}");

        using var doc = JsonDocument.Parse(File.ReadAllText(bundlePath));
        var bundled = doc.RootElement.GetProperty("icons").EnumerateObject()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        var registered = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(src, "*TabRegistration.cs", SearchOption.AllDirectories)) {
            var text = File.ReadAllText(file);
            foreach (Match m in IconKeyAssignment.Matches(text))
                registered.TryAdd(m.Groups[1].Value, file);
            foreach (Match m in TabRegistrationIconArgument.Matches(text))
                registered.TryAdd(m.Groups[1].Value, file);
        }

        Assert.NotEmpty(registered);

        var missing = registered
            .Where(kv => !bundled.Contains(kv.Key))
            .Select(kv => $"{kv.Key} ({Path.GetRelativePath(RepoRoot, kv.Value)})")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        Assert.True(missing.Length == 0,
            "Rail tab icons are registered with KitLibHost but missing from the Hud icon bundle. "
            + "Add the registering module to the tab roots in scripts/shake_icons.py and re-run it:\n  "
            + string.Join("\n  ", missing));
    }
}
