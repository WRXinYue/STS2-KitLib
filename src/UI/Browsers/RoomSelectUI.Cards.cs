using System;
using KitLib.Actions;
using KitLib.Icons;
using Godot;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.UI;

internal static partial class RoomSelectUI {
    private static readonly Color AncientAccent = new(0.90f, 0.65f, 0.20f);

    private static Control BuildRoomCard(RoomEntry entry, Label warnLabel, Label statusLabel) {
        var card = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var cardStyle = new StyleBoxFlat {
            BgColor = KitLibTheme.ButtonBgNormal,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            BorderWidthLeft = 3,
            BorderColor = entry.Accent with { A = 0.6f },
        };
        card.AddThemeStyleboxOverride("panel", cardStyle);
        card.MouseFilter = Control.MouseFilterEnum.Stop;
        card.AddChild(BuildCardBody(
            entry.Icon,
            entry.Accent,
            I18N.T(entry.NameKey, entry.NameFallback),
            I18N.T(entry.DescKey, entry.DescFallback)));

        WireCardHover(card, cardStyle, entry.Accent);

        var capturedEntry = entry;
        card.GuiInput += evt => {
            if (evt is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                return;

            if (!RoomActions.IsRunInProgress) {
                warnLabel.Visible = true;
                statusLabel.Text = "";
                return;
            }

            warnLabel.Visible = false;
            bool ok = RoomActions.TryEnterRoom(capturedEntry.Type);
            statusLabel.Text = ok
                ? I18N.T("room.entered", "Entering: {0}", I18N.T(capturedEntry.NameKey, capturedEntry.NameFallback))
                : I18N.T("room.error", "Failed to enter room.");
        };

        return card;
    }

    private static Control BuildAncientEntryCard(Action onOpen) {
        var card = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var cardStyle = new StyleBoxFlat {
            BgColor = KitLibTheme.ButtonBgNormal,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            BorderWidthLeft = 3,
            BorderColor = AncientAccent with { A = 0.6f },
        };
        card.AddThemeStyleboxOverride("panel", cardStyle);
        card.MouseFilter = Control.MouseFilterEnum.Stop;
        card.AddChild(BuildCardBody(
            MdiIcon.CalendarStar,
            AncientAccent,
            I18N.T("room.type.ancients", "Ancient Ones"),
            I18N.T("room.desc.ancients", "Visit an Ancient — dialogue and blessings.")));

        WireCardHover(card, cardStyle, AncientAccent);

        card.GuiInput += evt => {
            if (evt is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                return;
            onOpen();
        };

        return card;
    }

    private static MarginContainer BuildCardBody(MdiIcon icon, Color accent, string title, string description) {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        margin.MouseFilter = Control.MouseFilterEnum.Ignore;

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);
        hbox.MouseFilter = Control.MouseFilterEnum.Ignore;

        hbox.AddChild(new TextureRect {
            Texture = icon.Texture(20, accent),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(24, 24),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });

        var textCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        textCol.AddThemeConstantOverride("separation", 2);
        textCol.MouseFilter = Control.MouseFilterEnum.Ignore;

        var nameLabel = new Label { Text = title };
        nameLabel.AddThemeFontSizeOverride("font_size", 13);
        nameLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        textCol.AddChild(nameLabel);

        var descLabel = new Label {
            Text = description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        descLabel.AddThemeFontSizeOverride("font_size", 11);
        descLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        descLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        textCol.AddChild(descLabel);

        hbox.AddChild(textCol);

        hbox.AddChild(new TextureRect {
            Texture = MdiIcon.ChevronRight.Texture(16, KitLibTheme.Subtle),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(20, 20),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });

        margin.AddChild(hbox);
        return margin;
    }

    private static void WireCardHover(PanelContainer card, StyleBoxFlat cardStyle, Color accent) {
        card.MouseEntered += () => {
            cardStyle.BgColor = KitLibTheme.ButtonBgHover;
            cardStyle.BorderColor = accent with { A = 0.90f };
        };
        card.MouseExited += () => {
            cardStyle.BgColor = KitLibTheme.ButtonBgNormal;
            cardStyle.BorderColor = accent with { A = 0.60f };
        };
    }

    private static Button BuildAncientExtensionHeader(VBoxContainer extVbox, out Button titleBtn) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var backBtn = new Button {
            Text = I18N.T("room.ancients.back", "Back"),
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, 32),
        };
        var flat = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 6,
        };
        foreach (var s in new[] { "normal", "hover", "pressed", "focus" })
            backBtn.AddThemeStyleboxOverride(s, flat);
        backBtn.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        backBtn.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(backBtn);

        titleBtn = new Button {
            Text = I18N.T("room.section.ancients", "Ancient Ones"),
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, 32),
        };
        foreach (var s in new[] { "normal", "hover", "pressed", "focus" })
            titleBtn.AddThemeStyleboxOverride(s, flat);
        titleBtn.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        titleBtn.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(titleBtn);
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        extVbox.AddChild(row);
        extVbox.AddChild(new ColorRect {
            CustomMinimumSize = new Vector2(0, 1),
            Color = KitLibTheme.Separator,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        });

        return backBtn;
    }

    private static void BuildNavTab(VBoxContainer vbox, string title) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 0);

        var tab = new Button { Text = title, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 32) };
        var flat = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 4,
            ContentMarginBottom = 6,
        };
        foreach (var s in new[] { "normal", "hover", "pressed", "focus" })
            tab.AddThemeStyleboxOverride(s, flat);
        tab.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        tab.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(tab);
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        vbox.AddChild(row);
        vbox.AddChild(new ColorRect {
            CustomMinimumSize = new Vector2(0, 1),
            Color = KitLibTheme.Separator,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        });
    }
}
