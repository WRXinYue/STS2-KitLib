namespace KitLib.Abstractions.Modding;

/// <summary>
/// <para xml:lang="en">Player-facing labels for mods that ship <see cref="KitLibCompatDocument.FileName"/>.</para>
/// <para xml:lang="zh-CN">携带 <see cref="KitLibCompatDocument.FileName"/> 的内容 mod 在 UI 中的显示名。</para>
/// </summary>
public static class KitLibCompatDisplay {
    public const string SidebarBadge = "(KitLib)";

    public static string FormatSidebarDisplayName(string? displayName, string? modDirectory) {
        if (string.IsNullOrWhiteSpace(displayName))
            return displayName ?? "";
        if (!HasCompatSidecar(modDirectory))
            return displayName;
        return AppendBadge(displayName);
    }

    public static bool HasCompatSidecar(string? modDirectory) {
        if (string.IsNullOrWhiteSpace(modDirectory))
            return false;
        return KitLibCompatTomlReader.TryReadFile(modDirectory, out var document)
               && document is { HasConstraints: true };
    }

    public static string AppendBadge(string displayName) {
        var trimmed = displayName.TrimEnd();
        if (trimmed.EndsWith(SidebarBadge, StringComparison.Ordinal))
            return displayName;
        return $"{trimmed} {SidebarBadge}";
    }
}
