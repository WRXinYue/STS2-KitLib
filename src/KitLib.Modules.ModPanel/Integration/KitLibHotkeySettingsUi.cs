using System;
using System.Collections.Generic;
using Godot;
using KitLib.Host;
using KitLib.Settings;
using KitLib.UI;

namespace KitLib.Integration;

internal static class KitLibHotkeySettingsUi {
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
        (HotkeyActionId.TogglePerfHud, "hotkeys.togglePerfHud", "Performance overlay"),
    };

    private static readonly Dictionary<string, Button> BindingButtons = new(StringComparer.Ordinal);

    internal static Control BuildSection() {
        var col = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        col.AddThemeConstantOverride("separation", 8);
        BindingButtons.Clear();

        col.AddChild(KitLibNativeModSettingsUi.CreateBoolToggle(
            I18N.T("settings.hotkeysEnabled", "Enable keyboard shortcuts"),
            null,
            () => SettingsStore.Current.HotkeysEnabled,
            enabled => {
                SettingsStore.SetHotkeysEnabled(enabled);
                NotifyChanged();
            }));

        foreach (var (actionId, labelKey, labelFallback) in Rows)
            col.AddChild(CreateBindingRow(actionId, labelKey, labelFallback));

        var resetBtn = new Button {
            Text = I18N.T("hotkeys.reset", "Reset shortcuts to defaults"),
            FocusMode = Control.FocusModeEnum.All,
        };
        DevModeFormChrome.ApplyAccentPillButton(resetBtn);
        resetBtn.Pressed += () => {
            CancelCapture();
            SettingsStore.ResetHotkeys();
            NotifyChanged();
            RefreshAllBindingButtons();
        };
        col.AddChild(resetBtn);

        return col;
    }

    internal static void CancelCapture() => HotkeyCapture.Cancel();

    private static Control CreateBindingRow(string actionId, string labelKey, string labelFallback) {
        var bindBtn = new Button {
            CustomMinimumSize = new Vector2(DevModeFormChrome.Metrics.ChoiceRowMinWidth,
                DevModeFormChrome.Metrics.ValueColumnMinHeight),
            FocusMode = Control.FocusModeEnum.All,
        };
        StyleBindingButton(bindBtn, listening: false);
        DevModeFormChrome.WireRoundedFieldFocusMotion(bindBtn);
        UpdateBindingButtonText(bindBtn, actionId);
        bindBtn.Pressed += () => BeginListening(actionId, bindBtn);
        BindingButtons[actionId] = bindBtn;

        return DevModeFormChrome.CreateLabeledValueRow(
            I18N.T(labelKey, labelFallback),
            null,
            bindBtn);
    }

    private static void BeginListening(string actionId, Button bindBtn) {
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

            NotifyChanged();
        });
    }

    private static void NotifyChanged() => KitLibHost.NotifyHotkeySettingsChanged?.Invoke();

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
        var sb = listening
            ? new StyleBoxFlat {
                BgColor = KitLibTheme.Accent,
                BorderColor = KitLibTheme.Accent,
                BorderWidthBottom = 2,
                BorderWidthTop = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                ContentMarginLeft = 11,
                ContentMarginRight = 11,
                ContentMarginTop = 7,
                ContentMarginBottom = 7,
            }
            : DevModeFormChrome.RoundedField(false);
        var hover = listening ? sb : DevModeFormChrome.RoundedField(true);
        btn.AddThemeStyleboxOverride("normal", sb);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus", hover);
        btn.AddThemeFontSizeOverride("font_size", 14);
        btn.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
    }
}
