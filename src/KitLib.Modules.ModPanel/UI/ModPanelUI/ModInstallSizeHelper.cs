using System;
using System.Collections.Generic;
using System.IO;

namespace KitLib.UI;

internal static class ModInstallSizeHelper {
    private static readonly Dictionary<string, long> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static string FormatDetailLabel(string? installPath) {
        var bytes = TryMeasureBytes(installPath);
        if (bytes < 0)
            return "";
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024f:0.#} KB";
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / (1024f * 1024f):0.#} MB";
        return $"{bytes / (1024f * 1024f * 1024f):0.#} GB";
    }

    public static string FormatCompactLabel(string? installPath) {
        var bytes = TryMeasureBytes(installPath);
        if (bytes < 0)
            return "—";
        if (bytes < 1024)
            return $"{bytes}B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024}K";
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / (1024f * 1024f):0.#}M";
        return $"{bytes / (1024f * 1024f * 1024f):0.#}G";
    }

    private static long TryMeasureBytes(string? installPath) {
        if (string.IsNullOrWhiteSpace(installPath))
            return -1;
        var path = installPath.Trim();
        if (Cache.TryGetValue(path, out var cached))
            return cached;
        long total = -1;
        try {
            if (!Directory.Exists(path))
                return -1;
            total = MeasureDirectory(path);
            Cache[path] = total;
        }
        catch {
            return -1;
        }
        return total;
    }

    private static long MeasureDirectory(string root) {
        long total = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) {
            try {
                total += new FileInfo(file).Length;
            }
            catch {
                // Skip unreadable files.
            }
        }
        return total;
    }
}
