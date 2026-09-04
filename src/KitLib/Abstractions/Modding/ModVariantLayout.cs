namespace KitLib.Abstractions.Modding;

/// <summary>
/// On-disk layout for dual STS2 API packs: thin loader at the mod root plus
/// <c>lib/&lt;api&gt;/&lt;ModId&gt;.dll</c> and <c>compat-target.txt</c> (same as KitLib host).
/// </summary>
public static class ModVariantLayout {
    public const string LibDirectoryName = KitLibHostPaths.LibDirectoryName;

    public const string CompatTargetMarkerName = KitLibHostPaths.CompatTargetMarkerName;

    public static string ImplementationFileName(string modId) => $"{modId}.dll";

    public static string VariantRelativeImplementationPath(string modId, string compatTarget) =>
        $"{LibDirectoryName}/{compatTarget}/{ImplementationFileName(modId)}";
}
