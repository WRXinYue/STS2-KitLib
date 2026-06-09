using System.Collections.Generic;
using System.Text;

namespace KitLib.Abstractions.Modding;

/// <summary>Pure diagnostic log lines for ModPanel sidebar/embed probes (no Godot).</summary>
public static class ModPanelDiagnosticLog {
    public const string Prefix = "[ModPanelDiag]";

    public static string FormatOpen(ModPanelOpenReport report)
        => $"{Prefix} open: {FormatOpenPayload(report)}";

    public static string FormatOpenPayload(ModPanelOpenReport report) {
        var sb = new StringBuilder();
        sb.Append($"expectedRows={report.ExpectedRowCount}");
        sb.Append($", initialMod={report.InitialSelectedModId}");
        sb.Append($", rawLoaded={report.RawLoadedModCount}");
        sb.Append($", catalogSnapshot={report.CatalogSnapshotCount}");
        sb.Append($", embed={report.EmbedProbe.Status}");
        sb.Append($", submenuAlive={report.SubmenuAlive}");
        if (report.OrderedModIds.Count > 0)
            sb.Append($", modIds=[{string.Join(", ", report.OrderedModIds)}]");
        if (!string.IsNullOrWhiteSpace(report.EmbedProbe.Detail))
            sb.Append($", embedDetail={report.EmbedProbe.Detail}");
        return sb.ToString();
    }

    public static string FormatLayout(ModPanelLayoutSnapshot snapshot)
        => $"{Prefix} layout: {FormatLayoutPayload(snapshot)}";

    public static string FormatLayoutPayload(ModPanelLayoutSnapshot snapshot) {
        if (snapshot.ShellDisposed)
            return "shell disposed before layout probe";
        var sb = new StringBuilder();
        sb.Append($"shellSize={snapshot.ShellSize}");
        if (snapshot.ScrollMissing)
            sb.Append(", scroll=missing");
        else {
            sb.Append($", scrollSize={snapshot.ScrollSize}");
            sb.Append($", scrollVisible={snapshot.ScrollVisible}");
        }
        if (snapshot.ContentChildren is int contentChildren)
            sb.Append($", contentChildren={contentChildren}");
        if (snapshot.ModListChildren is int modListChildren)
            sb.Append($", modListChildren={modListChildren}");
        sb.Append($", modSections={snapshot.ModSectionCount}");
        return sb.ToString();
    }

    public static IReadOnlyList<string> CollectOpenWarnings(ModPanelOpenReport report) {
        var warnings = new List<string>();
        if (report.ExpectedRowCount == 0 && report.RawLoadedModCount > 0)
            warnings.Add($"{Prefix} loaded mods exist but catalog snapshot is empty — check manifest id fields.");
        if (report.ExpectedRowCount > 0 && report.EmbedProbe.Status != ModPanelEmbedHostStatus.Ready)
            warnings.Add(
                $"{Prefix} sidebar expects {report.ExpectedRowCount} rows but embed host is {report.EmbedProbe.Status}.");
        return warnings;
    }

    public static string FormatControllerContext(ModPanelControllerContext context)
        => $"{Prefix} controller: {FormatControllerContextPayload(context)}";

    public static string FormatControllerContextPayload(ModPanelControllerContext context) {
        var sb = new StringBuilder();
        sb.Append($"submenusOpen={context.SubmenusOpen}");
        sb.Append($", stackPeek={context.StackPeekType}");
        sb.Append($", isCurrent={context.IsCurrentSelf}");
        sb.Append($", usingController={context.UsingController}");
        sb.Append($", sidebarRows={context.SidebarRowCount}");
        if (!string.IsNullOrWhiteSpace(context.SelectedModId))
            sb.Append($", selectedMod={context.SelectedModId}");
        if (!string.IsNullOrWhiteSpace(context.FocusOwnerPath))
            sb.Append($", focus={context.FocusOwnerPath}");
        if (!string.IsNullOrWhiteSpace(context.MainMenuButtonsVisible))
            sb.Append($", mainMenuButtons={context.MainMenuButtonsVisible}");
        return sb.ToString();
    }

    public static string FormatControllerHints(bool usingController, bool hintsVisible, int tabCount)
        => $"{Prefix} controllerHints: usingController={usingController}, hintsVisible={hintsVisible}, tabCount={tabCount}";

    public static string FormatControllerInput(string action, bool handled, string? skipReason, string? selectedModId) {
        var sb = new StringBuilder();
        sb.Append($"{Prefix} controllerInput: action={action}, handled={handled}");
        if (!string.IsNullOrWhiteSpace(skipReason))
            sb.Append($", reason={skipReason}");
        if (!string.IsNullOrWhiteSpace(selectedModId))
            sb.Append($", selectedMod={selectedModId}");
        return sb.ToString();
    }

    public static IReadOnlyList<string> CollectLayoutWarnings(
        ModPanelOpenReport openReport,
        ModPanelLayoutSnapshot layout) {
        var warnings = new List<string>();
        if (layout.ShellDisposed)
            return warnings;
        if (openReport.ExpectedRowCount > 0 && layout.ModSectionCount == 0)
            warnings.Add(
                $"{Prefix} sidebar expects {openReport.ExpectedRowCount} rows but UI built 0 mod sections.");
        if (openReport.ExpectedRowCount > 0
            && layout.ModSectionCount > 0
            && openReport.ExpectedRowCount != layout.ModSectionCount)
            warnings.Add(
                $"{Prefix} row count mismatch: plan={openReport.ExpectedRowCount}, uiSections={layout.ModSectionCount}.");
        if (!layout.ScrollMissing && layout.ScrollVisible == false)
            warnings.Add($"{Prefix} sidebar scroll container is not visible.");
        return warnings;
    }
}

public readonly record struct ModPanelOpenReport(
    int ExpectedRowCount,
    string InitialSelectedModId,
    IReadOnlyList<string> OrderedModIds,
    int RawLoadedModCount,
    int CatalogSnapshotCount,
    ModPanelEmbedHostProbeResult EmbedProbe,
    bool SubmenuAlive);

public readonly record struct ModPanelControllerContext(
    bool SubmenusOpen,
    string StackPeekType,
    bool IsCurrentSelf,
    bool UsingController,
    int SidebarRowCount,
    string? SelectedModId,
    string? FocusOwnerPath,
    string? MainMenuButtonsVisible);

public readonly record struct ModPanelLayoutSnapshot(
    string ShellSize,
    string? ScrollSize,
    bool? ScrollVisible,
    int? ContentChildren,
    int? ModListChildren,
    int ModSectionCount,
    bool ScrollMissing = false,
    bool ShellDisposed = false);
