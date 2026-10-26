using System;
using Godot;
using KitLib.Settings;

namespace KitLib.UI;

/// <summary>
/// One-time "getting started" card for the KitLib settings pages: three keycap rows for the
/// controls players need first. Dismissal is persisted by version, so a later version can re-show it.
/// </summary>
internal static class KitLibWelcomeCard {
    internal const int Version = 1;

    private const int CornerRadius = 10;
    private const float ChipColumnWidth = 72f;

    /// <summary>First-run steps, in order: one shortcut each, taught by its own message then waited on.</summary>
    private static readonly (string Key, string Fallback, string ActionId)[] Walkthrough = {
        ("tutorial.tip.rail", "Press {0} to hide or show the sidebar", HotkeyActionId.ToggleRail),
        ("tutorial.tip.panel", "Press {0} to open KitLib settings", HotkeyActionId.OpenModPanel),
    };

    /// <summary>Returns the card, or null once the player has dismissed this version.</summary>
    internal static Control? TryCreate()
        => SettingsStore.ShouldShowWelcomeCard(Version) ? Build() : null;

    /// <summary>Builds the card unconditionally; <paramref name="onDismiss"/> replaces the default persistence.</summary>
    internal static Control Build(Action? onDismiss = null) {
        var card = new PanelContainer {
            Name = "KitLibWelcomeCard",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        card.AddThemeStyleboxOverride("panel", CardStyle());

        void Dismiss() {
            if (onDismiss != null)
                onDismiss();
            else
                SettingsStore.MarkWelcomeCardSeen(Version);
            card.QueueFree();
        }

        var body = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        body.AddThemeConstantOverride("separation", 8);
        card.AddChild(body);

        body.AddChild(Header(Dismiss));
        body.AddChild(Label(
            I18N.T("tutorial.welcome.title", "KitLib is ready"), 14, KitLibTheme.TextPrimary));
        body.AddChild(KeycapRow(HotkeyActionId.ToggleRail,
            I18N.T("tutorial.welcome.rail", "Hide or show every KitLib sidebar panel")));
        body.AddChild(KeycapRow(HotkeyActionId.OpenModPanel,
            I18N.T("tutorial.welcome.panel", "Open KitLib settings: modules, hotkeys, ring metrics")));
        body.AddChild(KeycapRow(HotkeyActionId.ClosePanel,
            I18N.T("tutorial.welcome.close", "Close the current panel or settings screen")));
        body.AddChild(Footer(Dismiss));
        return card;
    }

    private static Control Header(Action dismiss) {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);

        var kicker = Label(I18N.T("tutorial.welcome.kicker", "GETTING STARTED"), 10, KitLibTheme.Accent);
        kicker.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(kicker);

        var close = new Button {
            Text = "×",
            Flat = true,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(22f, 22f),
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            TooltipText = I18N.T("tutorial.welcome.dismiss", "Got it"),
        };
        close.AddThemeFontSizeOverride("font_size", 16);
        close.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        close.AddThemeColorOverride("font_hover_color", KitLibTheme.TextPrimary);
        close.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        close.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        close.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        close.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        KitLibLocaleFonts.ApplyControl(close);
        close.Pressed += dismiss;
        row.AddChild(close);
        return row;
    }

    private static Control KeycapRow(string actionId, string text) {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);

        var slot = new CenterContainer {
            CustomMinimumSize = new Vector2(ChipColumnWidth, 0f),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        slot.AddChild(KitLibHotkeyChip.Create(actionId));
        row.AddChild(slot);

        var desc = Label(text, 12, KitLibTheme.TextSecondary);
        desc.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        desc.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(desc);
        return row;
    }

    private static Control Footer(Action dismiss) {
        var row = new HBoxContainer {
            Alignment = BoxContainer.AlignmentMode.End,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var ok = new Button {
            Text = I18N.T("tutorial.welcome.dismiss", "Got it"),
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        DevModeFormChrome.ApplyAccentPillButton(ok);
        ok.Pressed += dismiss;
        row.AddChild(ok);
        return row;
    }

    /// <summary>
    /// <para xml:lang="en">Raises the first-run walkthrough step the player is on. Each step is pinned to the
    /// shortcut it teaches, so it holds until that key is actually pressed.</para>
    /// <para xml:lang="zh-CN">触发玩家当前所处的首次引导步骤。每步都钉住到它讲解的快捷键，直到该键真的被按下才前进。</para>
    /// </summary>
    internal static bool TryRaiseFirstRunTip() {
        if (!SettingsStore.ShouldShowFirstRunTip(Version))
            return false;

        int step = SettingsStore.FirstRunTipStep;
        if (step >= Walkthrough.Length) {
            SettingsStore.MarkFirstRunTipSeen(Version);
            return false;
        }

        RaiseStep(step);
        return true;
    }

    /// <summary>
    /// <para xml:lang="en">Called once the player has used the shortcut a step teaches: advance to the next step, or
    /// finish the walkthrough after the last one.</para>
    /// <para xml:lang="zh-CN">玩家按下某步讲解的快捷键后调用：前进到下一步，或走完最后一步后结束引导。</para>
    /// </summary>
    internal static void AdvanceFirstRunTip() {
        int step = SettingsStore.FirstRunTipStep + 1;
        if (step >= Walkthrough.Length) {
            SettingsStore.MarkFirstRunTipSeen(Version);
            return;
        }

        SettingsStore.SetFirstRunTipStep(step);
        RaiseStep(step);
    }

    private static void RaiseStep(int step) {
        var (key, fallback, actionId) = Walkthrough[step];
        KitLibNotificationCenter.Raise(new KitLibNotification(
            I18N.T(key, fallback, SettingsStore.GetHotkeyBinding(actionId).FormatLabel()),
            null, null, actionId));
    }

    private static Label Label(string text, int fontSize, Color color) {
        var label = new Label {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        KitLibLocaleFonts.ApplyControl(label);
        return label;
    }

    private static StyleBoxFlat CardStyle() {
        var border = KitLibTheme.PanelBorder;
        return new StyleBoxFlat {
            BgColor = KitLibTheme.ButtonBgNormal,
            BorderColor = new Color(border.R, border.G, border.B, Mathf.Clamp(border.A * 2.2f, 0.16f, 0.4f)),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = CornerRadius,
            CornerRadiusTopRight = CornerRadius,
            CornerRadiusBottomRight = CornerRadius,
            CornerRadiusBottomLeft = CornerRadius,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
        };
    }
}
