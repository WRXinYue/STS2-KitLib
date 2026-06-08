using System;

namespace KitLib;

/// <summary>
/// Built-in filter rules for the log viewer.
/// Each rule describes a known benign log pattern and why it can be safely ignored.
/// Rules are individually toggleable and track hit counts.
/// </summary>
internal static class LogSuppressor {
    public class Rule(string pattern, string description) {
        public readonly string Pattern = pattern;
        public readonly string Description = description;
        public bool Enabled = true;
        public int HitCount = 0;
    }

    public static readonly Rule[] BuiltInRules =
    {
        new("AtlasResourceLoader: Missing sprite",
            "Mod 图集资源缺失：该 Mod 未将贴图正确打包进图集，不影响游戏运行"),

        new("Asset not cached:",
            "资源未预热缓存：资源加载器的缓存未命中，不影响游戏运行"),

        new("[Assets] Missing resource path",
            "Mod 资源路径无效：Mod 引用了不存在的资源文件，不影响其他功能运行"),

        new("Found mod manifest file",
            "游戏扫描 Mod 目录时发现 JSON 文件（含非 Manifest 的数据文件），已知游戏行为"),

        new("missing the 'id' field",
            "非 Manifest 的 JSON 被游戏误识别为 Mod 配置文件，已知游戏 Bug"),

        new("warmup job failed",
            "KitLib 资源预热任务失败：通常为 Mod 中存在无效内容，不影响运行"),

        new("Limiting background FPS",
            "窗口失焦时限帧：游戏后台运行时的正常 INFO 日志"),

        new("Restored foreground FPS",
            "窗口重新聚焦时恢复帧率：正常 INFO 日志"),
    };

    /// <summary>
    /// Reset all hit counts (call before re-filtering a fresh snapshot).
    /// </summary>
    public static void ResetCounts() {
        foreach (var rule in BuiltInRules)
            rule.HitCount = 0;
    }

    /// <summary>
    /// Returns true if <paramref name="text"/> matches any <em>enabled</em> suppression rule.
    /// Increments the matched rule's <see cref="Rule.HitCount"/>.
    /// </summary>
    public static bool IsSuppressed(string text) {
        foreach (var rule in BuiltInRules) {
            if (!rule.Enabled) continue;
            if (text.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase)) {
                rule.HitCount++;
                return true;
            }
        }
        return false;
    }
}
