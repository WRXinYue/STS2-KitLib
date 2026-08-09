using System;
using System.IO;
using Godot;
using KitLib.Abstractions.Modding;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.UI;

internal static class ModPanelInstallSource {
    internal static string FormatSourceLabel(ModEntrySource source) => source switch {
        ModEntrySource.SteamWorkshop => I18N.T("modpanel.install.steam", "Workshop"),
        _ => I18N.T("modpanel.install.local", "Local"),
    };

    internal static string FormatLoadStatus(ModEntryLoadStatus status) => status switch {
        ModEntryLoadStatus.Loaded => I18N.T("modpanel.loadStatus.loaded", "loaded"),
        ModEntryLoadStatus.Failed => I18N.T("modpanel.loadStatus.failed", "failed"),
        ModEntryLoadStatus.Disabled => I18N.T("modpanel.loadStatus.disabled", "disabled"),
        ModEntryLoadStatus.DisabledDuplicate =>
            I18N.T("modpanel.loadStatus.duplicate", "duplicate (local overrides)"),
        ModEntryLoadStatus.AddedAtRuntime =>
            I18N.T("modpanel.loadStatus.addedAtRuntime", "added at runtime"),
        _ => I18N.T("modpanel.loadStatus.none", "not loaded"),
    };

    internal static string FormatShortPath(string? path, ModEntrySource source) {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        var normalized = path.Replace('\\', '/').TrimEnd('/');
        if (source == ModEntrySource.SteamWorkshop) {
            const string marker = "/workshop/content/";
            var idx = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) {
                var tail = normalized[(idx + marker.Length)..];
                var parts = tail.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    return $"workshop/{parts[1]}/{parts[^1]}";
            }
        }

        const string modsMarker = "/mods/";
        var modsIdx = normalized.LastIndexOf(modsMarker, StringComparison.OrdinalIgnoreCase);
        if (modsIdx >= 0)
            return "mods/" + normalized[(modsIdx + modsMarker.Length)..];

        return Path.GetFileName(normalized);
    }

    internal static ModEntrySource FromStsSource(ModSource source) => source switch {
        ModSource.SteamWorkshop => ModEntrySource.SteamWorkshop,
        _ => ModEntrySource.ModsDirectory,
    };

    internal static ModEntryLoadStatus FromStsLoadState(ModLoadState state) => state switch {
        ModLoadState.Loaded => ModEntryLoadStatus.Loaded,
        ModLoadState.Failed => ModEntryLoadStatus.Failed,
        ModLoadState.Disabled => ModEntryLoadStatus.Disabled,
        ModLoadState.DisabledDuplicate => ModEntryLoadStatus.DisabledDuplicate,
        ModLoadState.AddedAtRuntime => ModEntryLoadStatus.AddedAtRuntime,
        _ => ModEntryLoadStatus.None,
    };

    internal static bool TryOpenInstallFolder(string? installPath) {
        if (string.IsNullOrWhiteSpace(installPath))
            return false;
        try {
            if (!Directory.Exists(installPath))
                return false;
            OS.ShellOpen(installPath);
            return true;
        }
        catch {
            return false;
        }
    }
}
