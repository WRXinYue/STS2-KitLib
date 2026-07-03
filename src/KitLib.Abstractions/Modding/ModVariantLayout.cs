using KitLib.Abstractions.Compat;

namespace KitLib.Abstractions.Modding;

/// <summary>
/// <para xml:lang="en">On-disk layout for dual STS2 API variant bundles (thin loader DLL + flat lib/&lt;modId&gt;_&lt;version&gt;.dll files).</para>
/// <para xml:lang="zh-CN">双 STS2 API 变体发行包的磁盘布局约定（根目录薄 Loader + 扁平 lib/&lt;modId&gt;_&lt;version&gt;.dll）。</para>
/// </summary>
public static class ModVariantLayout {
    public const int ManifestSchema = 2;

    public const string LibDirectoryName = "lib";

    public static string ManifestFileName(string modId) =>
        $"{modId.ToLowerInvariant()}-variants.manifest";

    public static string ImplementationAssemblyFileName(string modId) =>
        $"{modId}.dll";

    public static string VariantFileName(string modId, string compatTarget) =>
        $"{modId}_{compatTarget}.dll";

    public static string VariantRelativePath(string modId, string compatTarget) =>
        $"{LibDirectoryName}/{VariantFileName(modId, compatTarget)}";

    public static bool TryParseVariantFileName(string modId, string fileName, out string compatTarget) {
        compatTarget = "";
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return false;

        var prefix = $"{modId}_";
        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var core = fileName[prefix.Length..^4];
        if (!Sts2GameVersion.TryParseCore(core, out _))
            return false;

        compatTarget = core;
        return true;
    }
}
