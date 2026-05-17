using System;
using DevMode;
using DevMode.Icons;
using DevMode.Panels;
using DevMode.Settings;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace DevMode.UI;

internal static partial class DevPanelUI {
    internal static void ShowSettingsOverlay(NGlobalUi globalUi, DevPanelActions actions) {
        var existing = ((Node)globalUi).GetNodeOrNull<Control>(SettingsRootName);
        if (existing != null) {
            ((Node)globalUi).RemoveChild(existing);
            existing.QueueFree();
        }

        var (root, _, vbox) = CreateOverlayRoot(globalUi, SettingsRootName, 480f);

        AddBrowserNavTab(vbox, I18N.T("panel.settings", "Settings"));

        var scroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var inner = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        inner.AddThemeConstantOverride("separation", 4);

        // ── Section: Appearance ──
        inner.AddChild(CreateSectionHeader(I18N.T("appearance.title", "Appearance")));
        inner.AddChild(CreateAppearanceSection(() => ShowSettingsOverlay(globalUi, actions)));

        // ── Section: Game ──
        inner.AddChild(CreateSectionHeader(I18N.T("panel.section.game", "Game")));

        var gameSpeedBtn = CreatePlainButton(I18N.T("panel.speed", "Speed: {0}", actions.GetGameSpeedLabel()), MdiIcon.SpeedometerMedium);
        gameSpeedBtn.Pressed += () => {
            actions.OnCycleGameSpeed();
            gameSpeedBtn.Text = I18N.T("panel.speed", "Speed: {0}", actions.GetGameSpeedLabel());
        };
        inner.AddChild(gameSpeedBtn);

        var skipAnimBtn = CreatePlainButton(I18N.T("panel.skipAnim", "Skip Anim: {0}", actions.GetSkipAnimLabel()), MdiIcon.AnimationPlay);
        skipAnimBtn.Pressed += () => {
            actions.OnToggleSkipAnim();
            skipAnimBtn.Text = I18N.T("panel.skipAnim", "Skip Anim: {0}", actions.GetSkipAnimLabel());
        };
        inner.AddChild(skipAnimBtn);

        scroll.AddChild(inner);
        vbox.AddChild(scroll);

        ((Node)globalUi).AddChild(root);
    }

    private static Control CreateAppearanceSection(Action rebuild) {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);

        // ── Mode toggle row: label + single icon button ──
        var modeRow = new HBoxContainer();
        modeRow.AddThemeConstantOverride("separation", 8);

        var modeLbl = new Label {
            Text = ThemeManager.IsDarkMode
                ? I18N.T("appearance.mode.dark", "Dark Mode")
                : I18N.T("appearance.mode.light", "Light Mode"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        modeLbl.AddThemeFontSizeOverride("font_size", 12);
        modeLbl.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
        modeRow.AddChild(modeLbl);

        // Icon button: shows sun (→ switch to dark) when in light mode,
        //              shows moon (→ switch to light) when in dark mode
        var modeIcon = ThemeManager.IsDarkMode ? MdiIcon.WeatherNight : MdiIcon.WeatherSunny;
        var modeBtn = new Button {
            CustomMinimumSize = new Vector2(36, 36),
            FocusMode = Control.FocusModeEnum.None,
            Icon = modeIcon.Texture(20, DevModeTheme.Accent),
            TooltipText = ThemeManager.IsDarkMode
                ? I18N.T("appearance.mode.light", "Light Mode")
                : I18N.T("appearance.mode.dark", "Dark Mode")
        };
        var modeBtnStyle = new StyleBoxFlat {
            BgColor = DevModeTheme.ButtonBgNormal,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };
        var modeBtnHover = new StyleBoxFlat {
            BgColor = DevModeTheme.ButtonBgHover,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };
        modeBtn.AddThemeStyleboxOverride("normal", modeBtnStyle);
        modeBtn.AddThemeStyleboxOverride("hover", modeBtnHover);
        modeBtn.AddThemeStyleboxOverride("pressed", modeBtnHover);
        modeBtn.AddThemeStyleboxOverride("focus", modeBtnStyle);
        modeBtn.Pressed += () => {
            ThemeManager.SetDarkMode(!ThemeManager.IsDarkMode);
            Callable.From(rebuild).CallDeferred();
        };
        modeRow.AddChild(modeBtn);
        col.AddChild(modeRow);

        // ── Dark theme selector ──
        var darkThemeBtn = CreatePlainButton(
            I18N.T("appearance.darkTheme", "Dark Theme: {0}",
                I18N.T("theme." + SettingsStore.Current.DarkThemeName.ToLowerInvariant(),
                    SettingsStore.Current.DarkThemeName)),
            MdiIcon.WeatherNight);
        darkThemeBtn.Pressed += () => {
            ThemeManager.CycleDarkTheme();
            Callable.From(rebuild).CallDeferred();
        };
        col.AddChild(darkThemeBtn);

        // ── Light theme selector ──
        var lightThemeBtn = CreatePlainButton(
            I18N.T("appearance.lightTheme", "Light Theme: {0}",
                I18N.T("theme." + SettingsStore.Current.LightThemeName.ToLowerInvariant(),
                    SettingsStore.Current.LightThemeName)),
            MdiIcon.WeatherSunny);
        lightThemeBtn.Pressed += () => {
            ThemeManager.CycleLightTheme();
            Callable.From(rebuild).CallDeferred();
        };
        col.AddChild(lightThemeBtn);

        var resetWidthBtn = CreatePlainButton(
            I18N.T("appearance.resetPanelWidths", "Reset saved panel widths"));
        resetWidthBtn.Pressed += () => {
            SettingsStore.Current.BrowserPanelWidths.Clear();
            SettingsStore.Save();
        };
        col.AddChild(resetWidthBtn);

        return col;
    }
}
