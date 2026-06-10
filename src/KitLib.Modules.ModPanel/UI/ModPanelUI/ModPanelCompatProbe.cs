using System;
using System.Collections.Generic;
using System.IO;
using KitLib.Abstractions.Host;
using KitLib.Abstractions.Modding;
using KitLib.Host;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.UI;

internal static class ModPanelCompatProbe {
    internal static KitLibCompatResult Evaluate(Mod? mod) {
        if (mod == null || string.IsNullOrWhiteSpace(mod.path))
            return new KitLibCompatResult();
        var directory = mod.path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!KitLibCompatTomlReader.TryReadFile(directory, out var document) || document == null)
            return new KitLibCompatResult();
        return KitLibCompatEvaluator.Evaluate(document, BuildRuntime());
    }

    internal static string? FormatWarning(KitLibCompatResult result) {
        if (result.IsCompatible)
            return null;
        var parts = new List<string>();
        if (result.Flags.HasFlag(KitLibCompatFlags.GameVersionMismatch)) {
            parts.Add(string.Format(
                I18N.T("modpanel.compat.gameVersion",
                    "Game version not supported (requires {0})."),
                string.Join(" or ", result.GameVersionRanges)));
        }
        if (result.Flags.HasFlag(KitLibCompatFlags.KitLibVersionMismatch)) {
            parts.Add(string.Format(
                I18N.T("modpanel.compat.kitlibVersion",
                    "Requires KitLib {0}."),
                string.Join(" or ", result.KitLibVersionRanges)));
        }
        if (result.Flags.HasFlag(KitLibCompatFlags.MissingKitLibModule)) {
            parts.Add(string.Format(
                I18N.T("modpanel.compat.missingModule",
                    "Requires KitLib module(s): {0}."),
                string.Join(", ", result.MissingModules)));
        }
        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    static KitLibCompatRuntime BuildRuntime() {
        string? kitLibVersion = null;
        foreach (var loaded in ModManagerLoadedMods.Enumerate()) {
            var id = loaded.manifest?.id;
            if (!string.Equals(id, KitLibModuleIds.Core, StringComparison.OrdinalIgnoreCase))
                continue;
            kitLibVersion = loaded.manifest?.version;
            break;
        }
        return new KitLibCompatRuntime {
            GameVersion = ModPanelModBanner.TryResolveGameBuildVersion(),
            KitLibVersion = kitLibVersion,
            IsModuleLoaded = KitLibHost.IsModuleLoaded,
        };
    }
}
