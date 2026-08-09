using System;
using System.IO;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using KitLib;
using KitLib.Feedback;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace KitLib.UI;

/// <summary>Adds a KitLib log-export row to vanilla <see cref="NErrorPopup"/> modals that show Report Bug.</summary>
internal static class ErrorPopupKitLibLogExportUI {
    private const string ButtonName = "KitLibLogExportButton";
    private const float ExtraRowHeight = 80f;
    private const float ReportBugButtonWidth = 180f;
    private const float ReportBugButtonHeight = 72f;
    // Vanilla vertical_popup.tscn bottom-anchored button row (room for ribbon bleed).
    private const float VanillaButtonTop = -152f;
    private const float VanillaButtonBottom = -80f;

    private static readonly AccessTools.FieldRef<NErrorPopup, bool> ShowReportBugRef =
        AccessTools.FieldRefAccess<NErrorPopup, bool>("_showReportBugButton");

    internal static void TryAttach(NErrorPopup popup) {
        try {
            if (!GodotObject.IsInstanceValid(popup) || !ShowReportBugRef(popup))
                return;

            var verticalPopup = popup.GetNodeOrNull<NVerticalPopup>("VerticalPopup");
            if (verticalPopup == null || verticalPopup.GetNodeOrNull<Control>(ButtonName) != null)
                return;

            ReserveBottomRow(verticalPopup);
            var exportBtn = InstantiateExportButton(verticalPopup);
            verticalPopup.AddChild(exportBtn);
            exportBtn.MoveToFront();
            // NPopupYesNoButton.SetText needs _Ready; finish wiring after the node enters the tree.
            Callable.From(() => FinishButtonSetup(exportBtn)).CallDeferred();
        }
        catch (Exception ex) {
            KitLog.Warn("Panel", $"Error-popup log export button failed: {ex.Message}");
        }
    }

    private static void FinishButtonSetup(NPopupYesNoButton exportBtn) {
        if (!GodotObject.IsInstanceValid(exportBtn))
            return;

        exportBtn.IsYes = true;
        exportBtn.SetText(I18N.T("log.export.error_popup", "Log Export (KitLib)"));

        var logs = FeedbackReportBuilder.ScanLogFiles();
        if (logs.Count == 0) {
            exportBtn.SetText(I18N.T("log.export.log.missing", "No game log file found under user://logs/."));
            exportBtn.SetEnabled(false);
            return;
        }

        exportBtn.Connect(
            NClickableControl.SignalName.Released,
            Callable.From<NButton>(_ => OnExportPressed(exportBtn)));
    }

    private static void ReserveBottomRow(NVerticalPopup verticalPopup) {
        verticalPopup.OffsetBottom += ExtraRowHeight;

        var description = verticalPopup.GetNodeOrNull<Control>("Description");
        if (description != null)
            description.OffsetBottom -= ExtraRowHeight;

        // Move Report Bug / Dismiss up so KitLib export can reuse the original bottom row inset.
        ShiftButtonRow(verticalPopup.YesButton);
        ShiftButtonRow(verticalPopup.NoButton);
    }

    private static void ShiftButtonRow(Control button) {
        button.OffsetTop -= ExtraRowHeight;
        button.OffsetBottom -= ExtraRowHeight;
    }

    private static NPopupYesNoButton InstantiateExportButton(NVerticalPopup verticalPopup) {
        var yesBtn = verticalPopup.YesButton;
        var scenePath = SceneHelper.GetScenePath("ui/abandon_run_yes_button");
        PackedScene scene = PreloadManager.Cache.ContainsKey(scenePath)
            ? PreloadManager.Cache.GetScene(scenePath)
            : ResourceLoader.Load<PackedScene>(scenePath);

        var exportBtn = scene.Instantiate<NPopupYesNoButton>();
        exportBtn.Name = ButtonName;
        exportBtn.CustomMinimumSize = new Vector2(ReportBugButtonWidth, ReportBugButtonHeight);
        exportBtn.AnchorLeft = yesBtn.AnchorLeft;
        exportBtn.AnchorRight = yesBtn.AnchorRight;
        exportBtn.AnchorTop = 1f;
        exportBtn.AnchorBottom = 1f;
        exportBtn.OffsetLeft = yesBtn.OffsetLeft;
        exportBtn.OffsetRight = yesBtn.OffsetRight;
        exportBtn.OffsetTop = VanillaButtonTop;
        exportBtn.OffsetBottom = VanillaButtonBottom;
        return exportBtn;
    }

    private static void OnExportPressed(NPopupYesNoButton btn) {
        if (!GodotObject.IsInstanceValid(btn) || !btn.IsEnabled)
            return;

        TaskHelper.RunSafely(RunExport(btn));
    }

    private static async Task RunExport(NPopupYesNoButton btn) {
        var defaultLabel = I18N.T("log.export.error_popup", "Log Export (KitLib)");
        btn.SetEnabled(false);
        btn.SetText(I18N.T("log.export.exporting", "Generating…"));

        var (zipPath, error) = await FeedbackReportUI.ExportDefaultZipAsync();

        if (!GodotObject.IsInstanceValid(btn))
            return;

        btn.SetEnabled(true);
        btn.SetText(defaultLabel);

        if (zipPath != null) {
            OS.ShellShowInFileManager(Path.GetDirectoryName(zipPath) ?? zipPath);
            return;
        }

        if (!string.IsNullOrEmpty(error))
            KitLog.Warn("Feedback", $"Error-popup log export failed: {error}");
    }
}
