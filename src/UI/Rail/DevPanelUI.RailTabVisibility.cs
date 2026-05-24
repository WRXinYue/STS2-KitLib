using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace DevMode.UI;

internal static partial class DevPanelUI {
    private static readonly Dictionary<string, string> BrowserOverlayRootByTabId = new() {
        ["devmode.cards"] = "DevModeCardBrowser",
        ["devmode.relics"] = "DevModeRelicBrowser",
        ["devmode.enemies"] = "DevModeEnemySelect",
        ["devmode.powers"] = "DevModePowerSelect",
        ["devmode.potions"] = "DevModePotionBrowser",
        ["devmode.events"] = "DevModeEventSelect",
        ["devmode.rooms"] = "DevModeRoomSelect",
        ["devmode.console"] = "DevModeConsole",
        ["devmode.ai"] = "DevModeAi",
        ["devmode.cheats"] = "DevModeCheats",
        ["devmode.enemyIntent"] = "DevModeEnemyIntent",
        ["devmode.combatStats"] = "DevModeCombatStats",
        ["devmode.presets"] = "DevModePresets",
        ["devmode.hooks"] = "DevModeHookConfig",
        ["devmode.scripts"] = "DevModeScripts",
        ["devmode.logs"] = LogCollector.LogViewerRootName,
        ["devmode.harmonyAnalysis"] = HarmonyAnalysisUI.RootName,
        ["devmode.frameworks"] = "DevModeFrameworkBridge",
        ["devmode.feedback"] = "DevModeFeedbackReport",
        ["devmode.save"] = "DevModeSaveLoad",
        ["devmode.manual"] = "DevModeManual",
        ["devmode.settings"] = "DevModeSettings",
    };

    /// <summary>
    /// Whether the browser overlay for a rail tab is still on screen.
    /// Unknown tabs return true so <see cref="Panels.DevPanelController.SwitchTo"/> keeps its no-op guard.
    /// </summary>
    internal static bool IsRailTabPanelVisible(NGlobalUi globalUi, string tabId) {
        if (!BrowserOverlayRootByTabId.TryGetValue(tabId, out var rootName))
            return true;
        return ((Node)globalUi).GetNodeOrNull<Control>(rootName) != null;
    }
}
