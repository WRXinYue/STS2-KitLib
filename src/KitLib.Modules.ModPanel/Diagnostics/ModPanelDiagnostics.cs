using System.Linq;
using Godot;
using KitLib.Abstractions.Modding;
using KitLib.Integration;
using KitLib.Modding;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.ModPanel.Diagnostics;

/// <summary>Structured logging to diagnose empty/invisible mod sidebar at runtime.</summary>
internal static class ModPanelDiagnostics {
    public static ModPanelOpenReport BuildOpenReport(
        ModPanelSidebarPlan plan,
        ModPanelEmbedHostProbeResult embedProbe,
        int rawLoadedModCount,
        int catalogSnapshotCount) {
        var submenuAlive = RitsuModSettingsEmbedHost.TryGetSubmenu() != null;
        return new ModPanelOpenReport(
            plan.ExpectedRowCount,
            plan.InitialSelectedModId,
            plan.OrderedMods.Select(m => m.Id).ToArray(),
            rawLoadedModCount,
            catalogSnapshotCount,
            embedProbe,
            submenuAlive);
    }

    public static void LogOpenReport(ModPanelOpenReport report) {
        MainFile.Logger.Info(ModPanelDiagnosticLog.FormatOpen(report));
        foreach (var warning in ModPanelDiagnosticLog.CollectOpenWarnings(report))
            MainFile.Logger.Warn(warning);
    }

    public static void LogSidebarLayoutDeferred(Control? shellRoot, ModPanelOpenReport? openReport = null) {
        if (shellRoot == null)
            return;
        Callable.From(() => LogSidebarLayout(shellRoot, openReport)).CallDeferred();
    }

    public static void LogSidebarLayout(Control shellRoot, ModPanelOpenReport? openReport = null) {
        var snapshot = CaptureLayoutSnapshot(shellRoot);
        MainFile.Logger.Info(ModPanelDiagnosticLog.FormatLayout(snapshot));
        if (openReport is { } report) {
            foreach (var warning in ModPanelDiagnosticLog.CollectLayoutWarnings(report, snapshot))
                MainFile.Logger.Warn(warning);
        }
    }

    public static ModPanelLayoutSnapshot CaptureLayoutSnapshot(Control shellRoot) {
        if (!GodotObject.IsInstanceValid(shellRoot))
            return new ModPanelLayoutSnapshot("", null, null, null, null, 0, ShellDisposed: true);
        var scroll = shellRoot.FindChild("ModPanelSidebarModScroll", true, false);
        var modList = shellRoot.FindChild("ModPanelSidebarModList", true, false);
        var content = scroll?.GetNodeOrNull<Control>("SidebarScrollInner")
            ?? scroll?.FindChild("Content", true, false);
        var sections = CountDescendantsNamed(shellRoot, "SidebarModSection_");
        if (scroll is not Control scrollCtrl) {
            return new ModPanelLayoutSnapshot(
                shellRoot.Size.ToString(),
                null,
                null,
                content is Control c ? c.GetChildCount() : null,
                modList is Control list ? list.GetChildCount() : null,
                sections,
                ScrollMissing: true);
        }
        return new ModPanelLayoutSnapshot(
            shellRoot.Size.ToString(),
            scrollCtrl.Size.ToString(),
            scrollCtrl.Visible,
            content is Control contentCtrl ? contentCtrl.GetChildCount() : null,
            modList is Control listCtrl ? listCtrl.GetChildCount() : null,
            sections);
    }

    public static int CountRawLoadedMods() {
        var n = 0;
        foreach (var _ in ModManagerLoadedMods.Enumerate())
            n++;
        return n;
    }

    static int CountDescendantsNamed(Node root, string namePrefix) {
        var n = 0;
        if (root.Name.ToString().StartsWith(namePrefix, System.StringComparison.Ordinal))
            n++;
        foreach (var child in root.GetChildren())
            n += CountDescendantsNamed(child, namePrefix);
        return n;
    }
}
