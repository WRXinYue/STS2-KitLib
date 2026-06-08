using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    private static readonly Dictionary<string, string> BrowserOverlayRootByTabId = new() {
        ["devmode.cards"] = "KitLibCardBrowser",
        ["devmode.relics"] = "KitLibRelicBrowser",
        ["devmode.enemies"] = "KitLibEnemySelect",
        ["devmode.powers"] = "KitLibPowerSelect",
        ["devmode.potions"] = "KitLibPotionBrowser",
        ["devmode.events"] = "KitLibEventSelect",
        ["devmode.rooms"] = "KitLibRoomSelect",
        ["devmode.console"] = "KitLibConsole",
        ["devmode.ai"] = "KitLibAi",
        ["devmode.cheats"] = "KitLibCheats",
        ["devmode.enemyIntent"] = "KitLibEnemyIntent",
        ["devmode.combatStats"] = "KitLibCombatStats",
        ["devmode.presets"] = "KitLibPresets",
        ["devmode.hooks"] = "KitLibHookConfig",
        ["devmode.scripts"] = "KitLibScripts",
        ["devmode.logs"] = LogCollector.LogViewerRootName,
        ["devmode.harmonyAnalysis"] = HarmonyAnalysisUI.RootName,
        ["devmode.frameworks"] = "KitLibFrameworkBridge",
        ["devmode.feedback"] = "KitLibFeedbackReport",
        ["devmode.save"] = "KitLibSaveLoad",
        ["devmode.manual"] = "KitLibManual",
        ["devmode.settings"] = "KitLibSettings",
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
