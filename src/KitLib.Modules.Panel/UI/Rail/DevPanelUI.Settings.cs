using System;
using Godot;
using KitLib;
using KitLib.Icons;
using KitLib.Integration;
using KitLib.Panels;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    internal static void ShowSettingsOverlay(NGlobalUi globalUi, DevPanelActions actions) {
        KitLibHotkeySettingsUi.CancelCapture();
        ((Node)globalUi).GetNodeOrNull<Control>(SettingsRootName)?.QueueFree();

        void FallbackClose() => ((Node)globalUi).GetNodeOrNull<Control>(SettingsRootName)?.QueueFree();

        var dual = CreateDualColumnOverlay(new DualColumnOverlayOptions {
            GlobalUi = globalUi,
            RootName = SettingsRootName,
            DualMetaKey = "dm_dual_settings",
            CarrierNodeName = "SettingsDualCarrier",
            MainWidthKey = SettingsRootName,
            ExtWidthKey = SettingsExtensionWidthKey,
            MainDefaultWidth = 480f,
            ExtDefaultWidth = 360f,
            FallbackClose = FallbackClose,
        });

        AddBrowserNavTab(dual.MainContent, I18N.T("panel.settings", "Settings"));

        var scroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var inner = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        inner.AddThemeConstantOverride("separation", 4);

        inner.AddChild(CreateSectionHeader(I18N.T("appearance.title", "Appearance")));
        inner.AddChild(CreateAppearanceSection(() => ShowSettingsOverlay(globalUi, actions)));

        inner.AddChild(CreateSectionHeader(I18N.T("panel.section.game", "Game")));
        inner.AddChild(CreateModPanelPrefsHint());

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

        CrashRecoveryPanelBuilder.AddToggleSection(inner, includeSectionHeader: true);

        var hotkeysBtn = CreatePlainButton(
            I18N.T("hotkeys.openInRunPanel", "Keyboard shortcuts…"),
            MdiIcon.ChevronRight);
        hotkeysBtn.Pressed += () => {
            if (dual.ExtSlot.Visible)
                dual.CloseExtension(() => KitLibHotkeySettingsUi.CancelCapture());
            else
                dual.OpenExtension();
        };
        inner.AddChild(hotkeysBtn);

        inner.AddChild(CreateRailLayoutSection(globalUi, actions));

        scroll.AddChild(inner);
        dual.MainContent.AddChild(scroll);

        AddBrowserNavTab(dual.ExtContent, I18N.T("settings.section.hotkeys", "Keyboard shortcuts"));

        var extScroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        extScroll.AddChild(KitLibHotkeySettingsUi.BuildSection(compact: true));
        dual.ExtContent.AddChild(extScroll);

        dual.Root.TreeExiting += () => KitLibHotkeySettingsUi.CancelCapture();
        dual.AttachToScene();
    }

    private static Control CreateAppearanceSection(Action rebuild) {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);

        var modeRow = new HBoxContainer();
        modeRow.AddThemeConstantOverride("separation", 8);

        var modeLbl = new Label {
            Text = ThemeManager.IsDarkMode
                ? I18N.T("appearance.mode.dark", "Dark Mode")
                : I18N.T("appearance.mode.light", "Light Mode"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        modeLbl.AddThemeFontSizeOverride("font_size", 12);
        modeLbl.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        modeRow.AddChild(modeLbl);

        var modeIcon = ThemeManager.IsDarkMode ? MdiIcon.WeatherNight : MdiIcon.WeatherSunny;
        var modeBtn = new Button {
            CustomMinimumSize = new Vector2(36, 36),
            FocusMode = Control.FocusModeEnum.None,
            Icon = modeIcon.Texture(20, KitLibTheme.Accent),
            TooltipText = ThemeManager.IsDarkMode
                ? I18N.T("appearance.mode.light", "Light Mode")
                : I18N.T("appearance.mode.dark", "Dark Mode"),
        };
        var modeBtnStyle = new StyleBoxFlat {
            BgColor = KitLibTheme.ButtonBgNormal,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
        };
        var modeBtnHover = new StyleBoxFlat {
            BgColor = KitLibTheme.ButtonBgHover,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
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

    private static Control CreateModPanelPrefsHint() {
        var hint = new Label {
            Text = I18N.T("settings.modPanelPrefsHint",
                "DevMode level, performance HUD, combat sidebar, and diagnostics: Main Menu → Mods → KitLib."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        return hint;
    }

    private static Control CreateRailLayoutSection(NGlobalUi globalUi, DevPanelActions actions) {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);

        col.AddChild(CreateSectionHeader(I18N.T("rail.title", "Sidebar")));

        var hint = new Label {
            Text = I18N.T("rail.hint", "Drag to reorder. Uncheck to hide a panel from the sidebar."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        col.AddChild(hint);

        col.AddChild(CreateRailGroupEditor(
            globalUi,
            DevPanelTabGroup.Primary,
            I18N.T("rail.group.primary", "Main panels")));

        col.AddChild(CreateRailGroupEditor(
            globalUi,
            DevPanelTabGroup.Utility,
            I18N.T("rail.group.utility", "Utility")));

        var resetBtn = CreatePlainButton(I18N.T("rail.reset", "Reset sidebar layout"));
        resetBtn.Pressed += () => {
            RailTabPreferences.ResetGroup(DevPanelTabGroup.Primary);
            RailTabPreferences.ResetGroup(DevPanelTabGroup.Utility);
            RebuildRail(globalUi);
            Callable.From(() => ShowSettingsOverlay(globalUi, actions)).CallDeferred();
        };
        col.AddChild(resetBtn);

        return col;
    }

    private static Control CreateRailGroupEditor(
        NGlobalUi globalUi,
        DevPanelTabGroup group,
        string title) {
        var block = new VBoxContainer();
        block.AddThemeConstantOverride("separation", 4);

        var sub = new Label { Text = title };
        sub.AddThemeFontSizeOverride("font_size", 11);
        sub.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        block.AddChild(sub);

        var list = new RailTabReorderList(
            group,
            RailTabPreferences.GetEditorEntries(group),
            orderedIds => {
                RailTabPreferences.SetOrder(group, orderedIds);
                RebuildRail(globalUi);
            },
            (tabId, visible) => {
                RailTabPreferences.SetVisible(tabId, visible);
                RebuildRail(globalUi);
            });
        block.AddChild(list);

        return block;
    }
}
