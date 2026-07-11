using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using KitLib;
using KitLib.Abstractions.Host;
using KitLib.Host;
using KitLib.Settings;
using KitLib.UI;

namespace KitLib.Integration;

/// <summary>
/// Hotkey settings section; capture follows official <c>NInputSettingsPanel</c> (_UnhandledKeyInput).
/// </summary>
internal partial class KitLibHotkeySettingsSection : VBoxContainer {
    internal static KitLibHotkeySettingsSection? Active { get; private set; }

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

    private readonly Dictionary<string, Button> _bindingButtons = new(StringComparer.Ordinal);
    private string? _listeningActionId;
    private bool _compact;

    internal KitLibHotkeySettingsSection() {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Ignore;
        SetProcessUnhandledInput(true);
        SetProcessUnhandledKeyInput(true);
    }

    public override void _EnterTree() => Active = this;

    public override void _ExitTree() {
        CancelListening();
        if (Active == this)
            Active = null;
    }

    internal void Build(bool compact = false) {
        _compact = compact;
        AddThemeConstantOverride("separation", compact ? 4 : 8);

        if (compact)
            AddChild(CreateCompactBoolToggle(
                I18N.T("settings.hotkeysEnabled", "Enable keyboard shortcuts"),
                () => SettingsStore.Current.HotkeysEnabled,
                enabled => {
                    SettingsStore.SetHotkeysEnabled(enabled);
                    NotifyChanged();
                }));
        else
            AddChild(KitLibNativeModSettingsUi.CreateBoolToggle(
                I18N.T("settings.hotkeysEnabled", "Enable keyboard shortcuts"),
                null,
                () => SettingsStore.Current.HotkeysEnabled,
                enabled => {
                    SettingsStore.SetHotkeysEnabled(enabled);
                    NotifyChanged();
                }));

        foreach (var (actionId, labelKey, labelFallback) in Rows)
            AddChild(CreateBindingRow(actionId, labelKey, labelFallback));

        AddChild(CreateSectionLabel(
            I18N.T("hotkeys.section.panels", "Open panel"),
            compact: _compact));

        foreach (var (actionId, labelKey, labelFallback) in GetRailTabHotkeyRows())
            AddChild(CreateBindingRow(actionId, labelKey, labelFallback));

        var resetBtn = new Button {
            Text = I18N.T("hotkeys.reset", "Reset shortcuts to defaults"),
            FocusMode = FocusModeEnum.All,
        };
        if (compact) {
            resetBtn.CustomMinimumSize = new Vector2(0, 32);
            resetBtn.AddThemeFontSizeOverride("font_size", 12);
            ModSettingsRitsuFormDevTheme.ApplyFieldControl(resetBtn);
        }
        else {
            DevModeFormChrome.ApplyAccentPillButton(resetBtn);
        }
        resetBtn.Pressed += () => {
            CancelListening();
            SettingsStore.ResetHotkeys();
            NotifyChanged();
            RefreshAllBindingButtons();
        };
        AddChild(resetBtn);
    }

    private static IEnumerable<(string ActionId, string LabelKey, string LabelFallback)> GetRailTabHotkeyRows() {
        var rows = new List<(string ActionId, string LabelKey, string LabelFallback)>();
        foreach (var tabObj in KitLibHost.GetAllTabs()) {
            if (tabObj is not KitLibTabDescriptor tab)
                continue;
            if (!tab.Id.StartsWith("devmode.", StringComparison.Ordinal))
                continue;
            if (!RailTabHotkeyDefaults.ByTabId.ContainsKey(tab.Id))
                continue;
            var labelKey = "panel." + tab.Id["devmode.".Length..];
            rows.Add((RailTabHotkeyActionId.ForTab(tab.Id), labelKey, tab.DisplayName));
        }
        rows.Sort((a, b) => string.Compare(a.LabelFallback, b.LabelFallback, StringComparison.OrdinalIgnoreCase));
        return rows;
    }

    private static Control CreateSectionLabel(string text, bool compact) {
        var label = new Label {
            Text = text,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        label.AddThemeFontSizeOverride("font_size", compact ? 11 : 13);
        label.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        return label;
    }

    internal static void CancelCapture() => Active?.CancelListening();

    internal static bool TryCaptureInputEvent(InputEventKey key, Viewport viewport) {
        var active = Active;
        if (active == null || active._listeningActionId == null)
            return false;

        active.ApplyCapturedKey(key);
        viewport.SetInputAsHandled();
        return true;
    }

    internal void CancelListening() {
        _listeningActionId = null;
        RefreshAllBindingButtons();
    }

    public override void _UnhandledInput(InputEvent inputEvent) => TryCaptureRebindKey(inputEvent);

    public override void _UnhandledKeyInput(InputEvent inputEvent) => TryCaptureRebindKey(inputEvent);

    void TryCaptureRebindKey(InputEvent inputEvent) {
        if (_listeningActionId == null)
            return;
        if (inputEvent is not InputEventKey { Pressed: true, Echo: false } key)
            return;

        ApplyCapturedKey(key);
        GetViewport()?.SetInputAsHandled();
    }

    void ApplyCapturedKey(InputEventKey key) {
        if (_listeningActionId == null)
            return;

        var actionId = _listeningActionId;
        CancelListening();

        if (key.Keycode == Key.Escape) {
            KitLog.Info("Hotkey", "Rebind cancelled (Esc).");
            return;
        }

        var binding = HotkeyBinding.From(key);
        KitLog.Info("Hotkey", $"Rebind capture: action={actionId} key={binding.FormatLabel()}");
        var reason = SettingsStore.TrySetHotkeyBinding(actionId, binding);
        if (reason != null) {
            KitLog.Info("Hotkey", $"Rebind rejected: {I18N.T(reason, reason)}");
            return;
        }

        KitLog.Info("Hotkey", $"Rebind saved: {actionId}={binding.FormatLabel()}");

        NotifyChanged();
    }

    private Control CreateBindingRow(string actionId, string labelKey, string labelFallback) {
        if (_compact)
            return CreateCompactBindingRow(actionId, labelKey, labelFallback);

        var bindBtn = CreateBindingButton(actionId);
        bindBtn.CustomMinimumSize = new Vector2(
            DevModeFormChrome.Metrics.ChoiceRowMinWidth,
            DevModeFormChrome.Metrics.ValueColumnMinHeight);

        return DevModeFormChrome.CreateLabeledValueRow(
            I18N.T(labelKey, labelFallback),
            null,
            bindBtn);
    }

    private Control CreateCompactBindingRow(string actionId, string labelKey, string labelFallback) {
        var row = new HBoxContainer {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddThemeConstantOverride("separation", 8);
        row.CustomMinimumSize = new Vector2(0, 30);

        var label = new Label {
            Text = I18N.T(labelKey, labelFallback),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipText = true,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        row.AddChild(label);

        var bindBtn = CreateBindingButton(actionId);
        bindBtn.CustomMinimumSize = new Vector2(108, 28);
        bindBtn.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        row.AddChild(bindBtn);
        return row;
    }

    private Button CreateBindingButton(string actionId) {
        var bindBtn = new Button { FocusMode = FocusModeEnum.All };
        bindBtn.SetMeta(KitLibHotkeySettingsUi.BindingButtonMeta, true);
        StyleBindingButton(bindBtn, listening: false);
        UpdateBindingButtonText(bindBtn, actionId);
        bindBtn.Pressed += () => {
            KitLog.Info("Hotkey", $"Rebind listening: {actionId}");
            BeginListening(actionId, bindBtn);
        };
        _bindingButtons[actionId] = bindBtn;
        return bindBtn;
    }

    private static Control CreateCompactBoolToggle(string label, Func<bool> get, Action<bool> set) {
        var row = new HBoxContainer {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddThemeConstantOverride("separation", 8);
        row.CustomMinimumSize = new Vector2(0, 30);

        var title = new Label {
            Text = label,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipText = true,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        title.AddThemeFontSizeOverride("font_size", 12);
        title.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        row.AddChild(title);

        var check = new CheckBox {
            ButtonPressed = get(),
            FocusMode = FocusModeEnum.All,
        };
        check.Toggled += on => set(on);
        row.AddChild(check);
        return row;
    }

    private void BeginListening(string actionId, Button bindBtn) {
        CancelListening();
        _listeningActionId = actionId;
        StyleBindingButton(bindBtn, listening: true);
        bindBtn.Text = I18N.T("hotkeys.listening", "Listening…");
    }

    private static void NotifyChanged() => KitLibHost.NotifyHotkeySettingsChanged?.Invoke();

    private void RefreshAllBindingButtons() {
        foreach (var (actionId, btn) in _bindingButtons) {
            if (!GodotObject.IsInstanceValid(btn))
                continue;
            StyleBindingButton(btn, _listeningActionId == actionId);
            UpdateBindingButtonText(btn, actionId);
        }
    }

    private void UpdateBindingButtonText(Button btn, string actionId) {
        if (_listeningActionId == actionId)
            return;
        btn.Text = SettingsStore.GetHotkeyBinding(actionId).FormatLabel();
    }

    private void StyleBindingButton(Button btn, bool listening) {
        var fontSize = _compact ? 11 : 14;
        if (listening) {
            var sb = new StyleBoxFlat {
                BgColor = new Color(KitLibTheme.Accent.R, KitLibTheme.Accent.G, KitLibTheme.Accent.B, 0.42f),
                BorderColor = KitLibTheme.Accent,
                BorderWidthBottom = 2,
                BorderWidthTop = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                ContentMarginLeft = _compact ? 8 : 11,
                ContentMarginRight = _compact ? 8 : 11,
                ContentMarginTop = _compact ? 4 : 7,
                ContentMarginBottom = _compact ? 4 : 7,
            };
            btn.AddThemeStyleboxOverride("normal", sb);
            btn.AddThemeStyleboxOverride("hover", sb);
            btn.AddThemeStyleboxOverride("pressed", sb);
            btn.AddThemeStyleboxOverride("focus", sb);
        }
        else {
            ModSettingsRitsuFormDevTheme.ApplyFieldControl(btn);
        }
        btn.AddThemeFontSizeOverride("font_size", fontSize);
        btn.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        btn.AddThemeColorOverride("font_hover_color", KitLibTheme.TextPrimary);
        btn.AddThemeColorOverride("font_pressed_color", KitLibTheme.TextPrimary);
    }
}
