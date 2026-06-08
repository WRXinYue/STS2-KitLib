using System;
using System.Collections.Generic;
using KitLib.Actions;
using Godot;

namespace KitLib.UI;

/// <summary>Expandable enchantment picker: header button + card-style icon grid inside the panel.</summary>
internal static class EnchantmentPickerUI {
    private const float TileMinWidth = 68f;
    private const float IconSize = 40f;
    private const float GridHSep = 6f;
    private const float GridVSep = 6f;
    private const float GridMaxHeight = 220f;
    private const int FrameRadius = 6;

    private static Color ColFrameBg => KitLibTheme.ButtonBgNormal;
    private static Color ColFrameHover => KitLibTheme.ButtonBgHover;
    private static Color ColFrameSelected => KitLibTheme.AccentAlpha;
    private static Color ColAccent => KitLibTheme.Accent;
    private static Color ColText => KitLibTheme.TextPrimary;
    private static Color ColSubtle => KitLibTheme.Subtle;

    public sealed class Options {
        public bool ShowModePicker { get; init; }
        public bool ShowForceButton { get; init; } = true;
        public bool ShowClearButton { get; init; }
        public bool StartExpanded { get; init; }
        public string? InitialTypeFullName { get; init; }
        public int InitialAmount { get; init; } = 1;
        public int InitialMode { get; init; }
        public string HeaderSubtitle { get; init; } = "";
    }

    public sealed class Model {
        public Control Root { get; internal set; } = null!;
        public Button HeaderButton { get; internal set; } = null!;
        public Control ExpandBody { get; internal set; } = null!;
        public GridContainer Grid { get; internal set; } = null!;
        public ScrollContainer GridScroll { get; internal set; } = null!;
        public SpinBox AmountSpin { get; internal set; } = null!;
        public Button ApplyButton { get; internal set; } = null!;
        public Button? ForceButton { get; internal set; }
        public Button? ClearButton { get; internal set; }
        public OptionButton? ModePicker { get; internal set; }
        public IReadOnlyList<CardEditActions.EnchantmentEntry> Entries { get; internal set; } = Array.Empty<CardEditActions.EnchantmentEntry>();

        internal Panel? SelectedTile;
        internal readonly Dictionary<string, (Control Root, Panel Frame)> Tiles = new(StringComparer.Ordinal);

        public string? SelectedTypeFullName { get; private set; }
        public bool IsExpanded { get; internal set; }
        public int Mode => ModePicker?.Selected ?? 2;

        public void SetExpanded(bool expanded) {
            IsExpanded = expanded;
            ExpandBody.Visible = expanded;
            RefreshHeader();
        }

        public void SetHeaderSubtitle(string subtitle) {
            HeaderSubtitle = subtitle;
            RefreshHeader();
        }

        public string HeaderSubtitle { get; internal set; } = "";

        public void SelectType(string? typeFullName) {
            SelectedTypeFullName = typeFullName;
            foreach (var (type, (_, frame)) in Tiles) {
                var selected = string.Equals(type, typeFullName, StringComparison.Ordinal);
                frame.AddThemeStyleboxOverride("panel", MakeFrameStyle(
                    selected ? ColFrameSelected : ColFrameBg,
                    ColAccent,
                    selected ? 0.95f : 0.40f));
            }
            SelectedTile = typeFullName != null && Tiles.TryGetValue(typeFullName, out var tile) ? tile.Frame : null;
        }

        public void SyncModeControls() {
            if (ModePicker == null) return;
            var setMode = ModePicker.Selected == 2;
            GridScroll.Modulate = setMode ? Colors.White : new Color(1, 1, 1, 0.45f);
            GridScroll.MouseFilter = setMode
                ? Control.MouseFilterEnum.Pass
                : Control.MouseFilterEnum.Ignore;
            AmountSpin.Editable = setMode;
        }

        internal void RefreshHeader() {
            var chevron = IsExpanded ? "▾" : "▸";
            HeaderButton.Text = string.Format(
                I18N.T("cardEdit.enchantHeader", "{0}  {1}  {2}"),
                I18N.T("cardEdit.enchantment", "Enchantment"),
                string.IsNullOrWhiteSpace(HeaderSubtitle)
                    ? I18N.T("cardEdit.noEnchant", "None")
                    : HeaderSubtitle,
                chevron);
        }
    }

    public static Model Build(Options options) {
        var entries = CardEditActions.GetEnchantmentEntries();
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 6);

        var model = new Model {
            Root = root,
            Entries = entries,
            HeaderSubtitle = options.HeaderSubtitle,
        };

        model.HeaderButton = new Button {
            CustomMinimumSize = new Vector2(0, 30),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Alignment = HorizontalAlignment.Left,
        };
        model.HeaderButton.Pressed += () => model.SetExpanded(!model.IsExpanded);
        root.AddChild(model.HeaderButton);

        model.ExpandBody = new VBoxContainer();
        model.ExpandBody.AddThemeConstantOverride("separation", 6);
        model.ExpandBody.Visible = options.StartExpanded;
        model.IsExpanded = options.StartExpanded;
        root.AddChild(model.ExpandBody);

        if (options.ShowModePicker) {
            var modeRow = new HBoxContainer();
            modeRow.AddThemeConstantOverride("separation", 4);
            modeRow.AddChild(new Label {
                Text = I18N.T("cardEdit.enchantMode", "Mode"),
                CustomMinimumSize = new Vector2(80, 0),
            });
            model.ModePicker = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            model.ModePicker.AddItem(I18N.T("cardEdit.enchantModeKeep", "Keep current"), 0);
            model.ModePicker.AddItem(I18N.T("cardEdit.enchantModeClear", "Clear"), 1);
            model.ModePicker.AddItem(I18N.T("cardEdit.enchantModeSet", "Set"), 2);
            model.ModePicker.Select(Math.Clamp(options.InitialMode, 0, 2));
            model.ModePicker.ItemSelected += _ => model.SyncModeControls();
            modeRow.AddChild(model.ModePicker);
            model.ExpandBody.AddChild(modeRow);
        }

        model.Grid = new GridContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        model.Grid.AddThemeConstantOverride("h_separation", (int)GridHSep);
        model.Grid.AddThemeConstantOverride("v_separation", (int)GridVSep);
        model.GridScroll = new ScrollContainer {
            CustomMinimumSize = new Vector2(0, GridMaxHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        model.GridScroll.AddChild(model.Grid);
        model.ExpandBody.AddChild(model.GridScroll);
        model.GridScroll.Resized += () => UpdateGridColumns(model);

        foreach (var entry in entries)
            AddTile(model, entry);

        var actionRow = new HBoxContainer();
        actionRow.AddThemeConstantOverride("separation", 4);
        actionRow.AddChild(new Label {
            Text = I18N.T("cardEdit.enchantAmount", "Amount"),
            CustomMinimumSize = new Vector2(48, 0),
        });
        model.AmountSpin = new SpinBox {
            MinValue = 1,
            MaxValue = 999,
            Value = options.InitialAmount,
            Step = 1,
            CustomMinimumSize = new Vector2(70, 26),
        };
        actionRow.AddChild(model.AmountSpin);

        model.ApplyButton = new Button {
            Text = I18N.T("cardEdit.applyEnchant", "Apply"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 26),
        };
        actionRow.AddChild(model.ApplyButton);

        if (options.ShowForceButton) {
            model.ForceButton = new Button {
                Text = I18N.T("cardEdit.forceEnchant", "Force"),
                CustomMinimumSize = new Vector2(50, 26),
            };
            actionRow.AddChild(model.ForceButton);
        }

        model.ExpandBody.AddChild(actionRow);

        if (options.ShowClearButton) {
            model.ClearButton = new Button {
                Text = I18N.T("cardEdit.clearEnchant", "Clear Enchantment"),
                CustomMinimumSize = new Vector2(0, 26),
            };
            model.ExpandBody.AddChild(model.ClearButton);
        }

        model.SelectType(options.InitialTypeFullName);
        model.SyncModeControls();
        model.RefreshHeader();

        return model;
    }

    private static void AddTile(Model model, CardEditActions.EnchantmentEntry entry) {
        var outer = new VBoxContainer {
            CustomMinimumSize = new Vector2(TileMinWidth, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = entry.DisplayName,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        outer.AddThemeConstantOverride("separation", 3);

        var frameCenter = new CenterContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, TileMinWidth),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var frameSize = TileMinWidth - 4f;
        var frameHost = new Control {
            CustomMinimumSize = new Vector2(frameSize, frameSize),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        var frame = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        frame.AddThemeStyleboxOverride("panel", MakeFrameStyle(ColFrameBg, ColAccent, 0.40f));
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        frameHost.AddChild(frame);

        var iconTex = CardEditActions.GetEnchantmentIcon(entry.TypeFullName);
        if (iconTex != null) {
            frameHost.AddChild(new TextureRect {
                Texture = iconTex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(IconSize, IconSize),
                Position = new Vector2((frameSize - IconSize) / 2f, (frameSize - IconSize) / 2f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        }
        else {
            frameHost.AddChild(new ColorRect {
                Color = ColAccent.Darkened(0.5f),
                CustomMinimumSize = new Vector2(IconSize, IconSize),
                Position = new Vector2((frameSize - IconSize) / 2f, (frameSize - IconSize) / 2f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        }

        frameCenter.AddChild(frameHost);
        outer.AddChild(frameCenter);

        var label = new Label {
            Text = entry.DisplayName,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 22),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 9);
        label.AddThemeColorOverride("font_color", ColText.Lerp(ColSubtle, 0.25f));
        outer.AddChild(label);

        var typeFullName = entry.TypeFullName;
        outer.MouseEntered += () => {
            if (model.SelectedTypeFullName != typeFullName)
                frame.AddThemeStyleboxOverride("panel", MakeFrameStyle(ColFrameHover, ColAccent, 0.70f));
        };
        outer.MouseExited += () => {
            if (model.SelectedTypeFullName != typeFullName)
                frame.AddThemeStyleboxOverride("panel", MakeFrameStyle(ColFrameBg, ColAccent, 0.40f));
        };
        outer.GuiInput += evt => {
            if (evt is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
                model.SelectType(typeFullName);
        };

        model.Tiles[typeFullName] = (outer, frame);
        model.Grid.AddChild(outer);
    }

    private static void UpdateGridColumns(Model model) {
        var available = model.GridScroll.Size.X;
        if (available <= 0) return;
        var cols = Math.Max(1, (int)((available + GridHSep) / (TileMinWidth + GridHSep)));
        if (model.Grid.Columns != cols)
            model.Grid.Columns = cols;
    }

    private static StyleBoxFlat MakeFrameStyle(Color bg, Color border, float borderAlpha) => new() {
        BgColor = bg,
        CornerRadiusTopLeft = FrameRadius,
        CornerRadiusTopRight = FrameRadius,
        CornerRadiusBottomLeft = FrameRadius,
        CornerRadiusBottomRight = FrameRadius,
        BorderWidthTop = 2,
        BorderWidthBottom = 2,
        BorderWidthLeft = 2,
        BorderWidthRight = 2,
        BorderColor = border with { A = borderAlpha },
    };
}
