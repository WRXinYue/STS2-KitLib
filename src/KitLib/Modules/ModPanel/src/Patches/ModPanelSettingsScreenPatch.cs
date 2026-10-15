using System;
using Godot;
using HarmonyLib;
using KitLib.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace KitLib.Patches;

[HarmonyPatch(typeof(NSettingsScreen))]
internal static class ModPanelSettingsScreenPatch {
    private const string GeneralSettingsResizeHookMeta = "kitlib_general_settings_content_resize_hook";

    [HarmonyPostfix]
    [HarmonyPatch("_Ready")]
    [HarmonyPatch(nameof(NSettingsScreen.OnSubmenuOpened))]
    [HarmonyPatch("OnSubmenuShown")]
    static void Postfix(NSettingsScreen __instance) {
        try {
            var line = EnsureEntryPoint(__instance);
            RefreshState(line);
            var generalPanel = __instance.GetNode<NSettingsPanel>("%GeneralSettings");
            ScheduleRefreshGeneralSettingsPanelSize(generalPanel);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib ModPanel: Failed to add settings entry: {ex.Message}");
        }
    }

    static MarginContainer EnsureEntryPoint(NSettingsScreen screen) {
        var panel = screen.GetNode<NSettingsPanel>("%GeneralSettings");
        var content = panel.Content;
        EnsureGeneralSettingsContentTracksChildAdds(content);

        if (TryGetEntryLine(content) is { } existing) {
            if (IsCurrentEntryLine(existing)) {
                if (content.GetNodeOrNull<Control>(ModPanelSettingsEntryLine.DividerNodeName) is { } existingDivider)
                    MoveEntryAboveNativeModSettings(content, existingDivider, existing);
                return existing;
            }

            RemoveStaleEntryNodes(content);
        }

        var divider = ModPanelSettingsEntryLine.CreateDivider();
        var line = ModPanelSettingsEntryLine.Create(() => OpenSubmenu(screen));
        content.AddChild(divider);
        content.AddChild(line);
        MoveEntryAboveNativeModSettings(content, divider, line);
        return line;
    }

    static void OpenSubmenu(NSettingsScreen screen) {
        var stack = screen.GetAncestorOfType<NSubmenuStack>();
        if (stack == null) {
            MainFile.Logger.Warn("KitLib ModPanel: No submenu stack for settings entry.");
            return;
        }
        ModPanelUI.Show(stack);
    }

    static void RefreshState(MarginContainer line) =>
        ModPanelSettingsEntryLine.RefreshButton(line);

    static void MoveEntryAboveNativeModSettings(VBoxContainer content, Control divider, MarginContainer line) {
        var anchor = content.GetNodeOrNull<Control>("ModdingDivider")
            ?? content.GetNodeOrNull<Control>("CreditsDivider");
        if (anchor == null)
            return;

        MoveChildBefore(content, line, anchor);
        MoveChildBefore(content, divider, line);
    }

    static void MoveChildBefore(VBoxContainer content, Control child, Control anchor) {
        var targetIndex = anchor.GetIndex();
        if (child.GetIndex() < targetIndex)
            targetIndex--;
        content.MoveChild(child, targetIndex);
    }

    static MarginContainer? TryGetEntryLine(VBoxContainer content) {
        var line = content.GetNodeOrNull<MarginContainer>(ModPanelSettingsEntryLine.LineNodeName);
        if (line == null || !GodotObject.IsInstanceValid(line) || line.GetParent() != content)
            return null;
        return line;
    }

    static bool IsCurrentEntryLine(MarginContainer line) =>
        line.GetNodeOrNull<ModPanelSettingsEntryButton>(
            $"ContentRow/{ModPanelSettingsEntryLine.ButtonNodeName}") != null;

    static void RemoveStaleEntryNodes(VBoxContainer content) {
        content.GetNodeOrNull<MarginContainer>(ModPanelSettingsEntryLine.LineNodeName)?.QueueFree();
        content.GetNodeOrNull<Control>(ModPanelSettingsEntryLine.DividerNodeName)?.QueueFree();
    }

    static void EnsureGeneralSettingsContentTracksChildAdds(VBoxContainer content) {
        if (content.HasMeta(GeneralSettingsResizeHookMeta))
            return;

        content.SetMeta(GeneralSettingsResizeHookMeta, true);
        content.ChildEnteredTree += child => {
            if (child.GetParentOrNull<VBoxContainer>() is not { } parentContent)
                return;
            if (parentContent.GetParentOrNull<NSettingsPanel>() is not { } panel)
                return;
            ScheduleRefreshGeneralSettingsPanelSize(panel);
        };
    }

    static void ScheduleRefreshGeneralSettingsPanelSize(NSettingsPanel panel) {
        Callable.From(() => {
            if (GodotObject.IsInstanceValid(panel))
                RefreshPanelSize(panel);
        }).CallDeferred();
    }

    static void RefreshPanelSize(NSettingsPanel panel) {
        if (!GodotObject.IsInstanceValid(panel))
            return;

        var content = panel.Content;
        if (!GodotObject.IsInstanceValid(content))
            return;

        content.QueueSort();

        var parent = panel.GetParent<Control>();
        if (parent == null)
            return;

        var parentSize = parent.Size;
        var minimumSize = content.GetMinimumSize();
        var stackedMinY = ComputeVBoxContentMinHeight(content);
        var needHeightY = Mathf.Max(minimumSize.Y, stackedMinY);
        const float minPadding = 50f;
        var width = content.Size.X > 1f ? content.Size.X : parentSize.X;
        panel.Size = needHeightY + minPadding >= parentSize.Y
            ? new Vector2(width, needHeightY + parentSize.Y * 0.4f)
            : new Vector2(width, needHeightY);
    }

    static float ComputeVBoxContentMinHeight(VBoxContainer box) {
        var sep = box.GetThemeConstant("separation");
        var y = 0f;
        var first = true;
        foreach (var node in box.GetChildren()) {
            if (node is not Control { Visible: true } c)
                continue;
            if (!first)
                y += sep;
            first = false;
            y += c.GetCombinedMinimumSize().Y;
        }
        return y;
    }
}
