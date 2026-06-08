using System;
using System.Collections.Generic;
using KitLib.Settings;
using Godot;

namespace KitLib.UI;

internal static class DevPanelHotkeySettingsUI {
    private static readonly (string ActionId, string LabelKey, string LabelFallback)[] Rows = {
        (HotkeyActionId.ToggleRail, "hotkeys.toggleRail", "Toggle sidebar"),
        (HotkeyActionId.ClosePanel, "hotkeys.closePanel", "Close panel"),
        (HotkeyActionId.NextTab, "hotkeys.nextTab", "Next tab"),
        (HotkeyActionId.PrevTab, "hotkeys.prevTab", "Previous tab"),
        (HotkeyActionId.LockRail, "hotkeys.lockRail", "Lock sidebar"),
        (HotkeyActionId.QuickSave, "hotkeys.quickSave", "Quick save"),
        (HotkeyActionId.QuickLoad, "hotkeys.quickLoad", "Quick load"),
        (HotkeyActionId.QuickReplayCombat, "hotkeys.quickReplayCombat", "Replay combat"),
        (HotkeyActionId.QuickReplayTurn, "hotkeys.quickReplayTurn", "Replay turn"),
    };

    private static readonly Dictionary<string, Button> BindingButtons = new(StringComparer.Ordinal);

    internal static Control BuildSection(Action rebuildSettings) {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        BindingButtons.Clear();

        col.AddChild(DevPanelUI.CreateSectionHeader(I18N.T("settings.section.hotkeys", "Keyboard shortcuts")));

        col.AddChild(DevPanelUI.CreateCheatToggle(
            I18N.T("settings.hotkeysEnabled", "Enable keyboard shortcuts"),
            null,
            () => SettingsStore.Current.HotkeysEnabled,
            enabled => {
                SettingsStore.SetHotkeysEnabled(enabled);
                DevPanelUI.RefreshPeekTabHotkeyHint();
                rebuildSettings();
            }));

        foreach (var (actionId, labelKey, labelFallback) in Rows) {
            col.AddChild(CreateBindingRow(actionId, labelKey, labelFallback, rebuildSettings));
        }

        var resetBtn = DevPanelUI.CreatePlainButton(I18N.T("hotkeys.reset", "Reset shortcuts to defaults"));
        resetBtn.Pressed += () => {
            HotkeyCapture.Cancel();
            SettingsStore.ResetHotkeys();
            DevPanelUI.RefreshPeekTabHotkeyHint();
            RefreshAllBindingButtons();
            rebuildSettings();
        };
        col.AddChild(resetBtn);

        return col;
    }

    internal static void CancelCapture() => HotkeyCapture.Cancel();

    private static Control CreateBindingRow(
        string actionId,
        string labelKey,
        string labelFallback,
        Action rebuildSettings) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.CustomMinimumSize = new Vector2(0, 32);

        var lbl = new Label {
            Text = I18N.T(labelKey, labelFallback),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true
        };
        lbl.AddThemeFontSizeOverride("font_size", 12);
        lbl.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        row.AddChild(lbl);

        var bindBtn = new Button {
            CustomMinimumSize = new Vector2(140, 28),
            FocusMode = Control.FocusModeEnum.None
        };
        bindBtn.AddThemeFontSizeOverride("font_size", 11);
        StyleBindingButton(bindBtn, listening: false);
        UpdateBindingButtonText(bindBtn, actionId);
        bindBtn.Pressed += () => BeginListening(actionId, bindBtn, rebuildSettings);
        BindingButtons[actionId] = bindBtn;
        row.AddChild(bindBtn);

        return row;
    }

    private static void BeginListening(string actionId, Button bindBtn, Action rebuildSettings) {
        if (HotkeyCapture.IsListening) {
            HotkeyCapture.Cancel();
            RefreshAllBindingButtons();
        }

        StyleBindingButton(bindBtn, listening: true);
        bindBtn.Text = I18N.T("hotkeys.listening", "Listening…");

        HotkeyCapture.Begin(actionId, (_, binding) => {
            RefreshAllBindingButtons();
            if (binding == null)
                return;

            var reason = SettingsStore.TrySetHotkeyBinding(actionId, binding);
            if (reason != null) {
                MainFile.Logger.Info($"Hotkey rebind rejected: {I18N.T(reason, reason)}");
                return;
            }

            if (actionId == HotkeyActionId.ToggleRail)
                DevPanelUI.RefreshPeekTabHotkeyHint();
            rebuildSettings();
        });
    }

    private static void RefreshAllBindingButtons() {
        foreach (var (actionId, btn) in BindingButtons) {
            if (!GodotObject.IsInstanceValid(btn))
                continue;
            StyleBindingButton(btn, HotkeyCapture.ListeningActionId == actionId);
            UpdateBindingButtonText(btn, actionId);
        }
    }

    private static void UpdateBindingButtonText(Button btn, string actionId) {
        if (HotkeyCapture.ListeningActionId == actionId)
            return;
        btn.Text = SettingsStore.GetHotkeyBinding(actionId).FormatLabel();
    }

    private static void StyleBindingButton(Button btn, bool listening) {
        var bg = listening ? KitLibTheme.Accent : KitLibTheme.PanelBg;
        var border = listening ? KitLibTheme.Accent : KitLibTheme.PanelBorder;
        var sb = new StyleBoxFlat {
            BgColor = bg,
            BorderColor = border,
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };
        btn.AddThemeStyleboxOverride("normal", sb);
        btn.AddThemeStyleboxOverride("hover", sb);
        btn.AddThemeStyleboxOverride("pressed", sb);
        btn.AddThemeStyleboxOverride("focus", sb);
        btn.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
    }
}
