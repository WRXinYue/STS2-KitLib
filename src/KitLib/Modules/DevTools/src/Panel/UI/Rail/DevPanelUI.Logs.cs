using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    /// <summary>
    /// The log panel is a standard DevPanelUI content panel with Logs + Combat tabs; the unified
    /// <see cref="DualColumnOverlayHandle.WireDragAndHide"/> provides drag + collapse. The root name
    /// stays <see cref="LogCollector.LogViewerRootName"/> (rail reveal / SyncLogViewerOpen depend on it).
    /// </summary>
    internal static void ShowLogsOverlay(NGlobalUi globalUi, bool expandLogExport = false) {
        GD.Print("[KitDevTools] ShowLogsOverlay entered");
        try {
            // Drop any same-named hidden cached node so it cannot stack with the new panel.
            DiscardOverlayImmediate(globalUi, LogCollector.LogViewerRootName);

            LogCollector.RefreshFileSnapshot();

            var dual = CreateMainOnlyDualOverlay(
                globalUi,
                LogCollector.LogViewerRootName,
                mainDefaultWidth: 880f,
                fallbackClose: () => DiscardOverlayImmediate(globalUi, LogCollector.LogViewerRootName),
                contentSeparation: 8,
                headerTitle: I18N.T("floatingLogCombat.title", "Dev Logs / Combat"));
            GD.Print("[KitDevTools] ShowLogsOverlay: dual overlay created");
            dual.AttachToScene();

            // ── Logs / Combat tab switching ──
            var logsChip = CreateFilterChip(I18N.T("floatingLogCombat.tab.logs", "Logs"), active: true);
            var combatChip = CreateFilterChip(I18N.T("floatingLogCombat.tab.combat", "Combat"), active: false);

            var tabRow = new HBoxContainer();
            tabRow.AddThemeConstantOverride("separation", 8);
            tabRow.AddChild(logsChip);
            tabRow.AddChild(combatChip);
            dual.MainContent.AddChild(tabRow);

            var logsVBox = new VBoxContainer {
                Visible = true,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };
            logsVBox.AddThemeConstantOverride("separation", 8);
            dual.MainContent.AddChild(logsVBox);

            var combatVBox = new VBoxContainer {
                Visible = false,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };
            combatVBox.AddThemeConstantOverride("separation", 8);
            dual.MainContent.AddChild(combatVBox);

            void SelectTab(bool logs) {
                logsVBox.Visible = logs;
                combatVBox.Visible = !logs;
                logsChip.ButtonPressed = logs;
                combatChip.ButtonPressed = !logs;
            }

            logsChip.Pressed += () => SelectTab(true);
            combatChip.Pressed += () => SelectTab(false);

            // Logs content (with its own export-extension button); closing uses the standard teardown.
            var exportBtn = LogViewerUI.BuildPanel(
                logsVBox,
                dual.Root,
                () => RequestCloseBrowserOverlay(
                    globalUi,
                    LogCollector.LogViewerRootName,
                    () => DiscardOverlayImmediate(globalUi, LogCollector.LogViewerRootName)));
            GD.Print("[KitDevTools] ShowLogsOverlay: LogViewerUI.BuildPanel OK");
            LogViewerUI.WireMainMenuLogExportExtension(dual.Root, dual.MainPanel, exportBtn, expandLogExport);

            // Combat live stats.
            CombatStatsUI.Attach(combatVBox);
            GD.Print("[KitDevTools] ShowLogsOverlay: CombatStatsUI.Attach OK");
        }
        catch (Exception ex) {
            GD.PrintErr($"[KitDevTools] ShowLogsOverlay THREW: {ex}");
        }
    }
}
