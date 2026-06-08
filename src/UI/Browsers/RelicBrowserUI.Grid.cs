using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Modding;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.UI;

internal static partial class RelicBrowserUI {
    private const float TileMinWidth = 110f;
    private const float IconFrameSize = 96f;
    private const float IconPad = 12f;
    private const float IconSize = IconFrameSize - IconPad * 2f;
    private const int FrameRadius = 20;
    private const int GridHSep = 12;   // horizontal gap between columns
    private const int GridVSep = 20;   // vertical gap between rows (bigger: name label adds height)
    private const int GridPadH = 18;
    private const int GridPadV = 16;
    private const int MaxColumns = 6;

    // Frame colors
    private static readonly Color ColFrameBg = new(0.13f, 0.13f, 0.17f, 0.70f);
    private static readonly Color ColFrameHover = new(0.17f, 0.17f, 0.22f, 0.85f);
    private static readonly Color ColFrameSelected = new(0.18f, 0.22f, 0.30f, 0.92f);

    private const float BorderAlphaRest = 0.22f;
    private const float BorderAlphaHover = 0.55f;
    private const float BorderAlphaSelected = 0.88f;

    private static Control CreateRelicTile(RelicModel relic, Player? player, State s) {
        var rarity = GetRelicRarity(relic);
        var rarityCol = RarityToColor(rarity);
        var name = GetRelicDisplayName(relic);

        var outer = new VBoxContainer {
            CustomMinimumSize = new Vector2(TileMinWidth, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = name
        };
        outer.AddThemeConstantOverride("separation", 6);

        // ── Icon frame (rounded square, app-icon style) ──

        var frameCenter = new CenterContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        var frameHost = new Control {
            CustomMinimumSize = new Vector2(IconFrameSize, IconFrameSize),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        var frame = new Panel();
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        frame.MouseFilter = Control.MouseFilterEnum.Ignore;
        var frameStyle = new StyleBoxFlat {
            BgColor = ColFrameBg,
            CornerRadiusTopLeft = FrameRadius,
            CornerRadiusTopRight = FrameRadius,
            CornerRadiusBottomLeft = FrameRadius,
            CornerRadiusBottomRight = FrameRadius,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderColor = RarityBorderColor(rarityCol, BorderAlphaRest)
        };
        frame.AddThemeStyleboxOverride("panel", frameStyle);
        frameHost.AddChild(frame);

        // Icon texture
        Texture2D? iconTex = null;
        try { iconTex = relic.Icon; } catch { }

        if (iconTex != null) {
            var iconRect = new TextureRect {
                Texture = iconTex,
                CustomMinimumSize = new Vector2(IconSize, IconSize),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorLeft = 0.5f,
                AnchorRight = 0.5f,
                AnchorTop = 0.5f,
                AnchorBottom = 0.5f,
                OffsetLeft = -IconSize / 2f,
                OffsetRight = IconSize / 2f,
                OffsetTop = -IconSize / 2f,
                OffsetBottom = IconSize / 2f
            };
            frameHost.AddChild(iconRect);
        }
        else {
            var fallback = new ColorRect {
                Color = rarityCol.Darkened(0.5f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorLeft = 0.5f,
                AnchorRight = 0.5f,
                AnchorTop = 0.5f,
                AnchorBottom = 0.5f,
                OffsetLeft = -IconSize / 2f,
                OffsetRight = IconSize / 2f,
                OffsetTop = -IconSize / 2f,
                OffsetBottom = IconSize / 2f
            };
            frameHost.AddChild(fallback);
        }

        // Owned badge — rounded dot at bottom-right of frame
        if (IsAllSource && player != null && IsRelicOwned(relic, player)) {
            var badge = new Panel {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorLeft = 1,
                AnchorRight = 1,
                AnchorTop = 1,
                AnchorBottom = 1,
                OffsetLeft = -18,
                OffsetRight = -4,
                OffsetTop = -18,
                OffsetBottom = -4
            };
            var badgeStyle = new StyleBoxFlat {
                BgColor = new Color(0.28f, 0.72f, 0.42f, 0.92f),
                CornerRadiusTopLeft = 7,
                CornerRadiusTopRight = 7,
                CornerRadiusBottomLeft = 7,
                CornerRadiusBottomRight = 7,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                BorderWidthTop = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.1f, 0.1f, 0.14f, 0.9f)
            };
            badge.AddThemeStyleboxOverride("panel", badgeStyle);
            frameHost.AddChild(badge);
        }

        frameCenter.AddChild(frameHost);
        outer.AddChild(frameCenter);

        // ── Name label (rarity-tinted, below the frame) ──

        var nameColor = rarity == RelicRarity.None
            ? KitLibTheme.Subtle
            : rarityCol.Lerp(KitLibTheme.TextPrimary, 0.45f);

        var label = new Label {
            Text = name,
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipText = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", nameColor);
        outer.AddChild(label);

        // ── Hover / click ──

        outer.MouseEntered += () => {
            if (s.SelectedRelic != relic)
                SetFrameStyle(frame, ColFrameHover, rarityCol, BorderAlphaHover);
        };
        outer.MouseExited += () => {
            if (s.SelectedRelic != relic)
                SetFrameStyle(frame, ColFrameBg, rarityCol, BorderAlphaRest);
        };
        outer.GuiInput += evt => {
            if (evt is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
                return;
            SelectTile(s, frame, relic, rarityCol);
            outer.AcceptEvent();
        };

        return outer;
    }

    // ── Frame styling ──

    private static Color RarityBorderColor(Color rarity, float alpha)
        => new(rarity.R, rarity.G, rarity.B, alpha);

    private static void SetFrameStyle(Panel frame, Color bg, Color rarityCol, float borderAlpha) {
        if (frame.GetThemeStylebox("panel") is StyleBoxFlat sb) {
            sb.BgColor = bg;
            sb.BorderColor = RarityBorderColor(rarityCol, borderAlpha);
        }
    }

    private static void SelectTile(State s, Panel frame, RelicModel relic, Color rarityCol) {
        if (s.SelectedBg != null)
            SetFrameStyle(s.SelectedBg, ColFrameBg, s.SelectedRarityCol, BorderAlphaRest);

        s.SelectedBg = frame;
        s.SelectedRelic = relic;
        s.SelectedRarityCol = rarityCol;
        SetFrameStyle(frame, ColFrameSelected, rarityCol, BorderAlphaSelected);
        ShowRightPanel(s, relic);
    }

    private static bool IsRelicOwned(RelicModel relic, Player player) {
        try {
            var relicId = ((AbstractModel)relic).Id;
            return player.Relics.Any(r => ((AbstractModel)r).Id == relicId);
        }
        catch { return false; }
    }

    // ── Grid operations ──

    private static List<RelicModel> GetRelics(State s) {
        if (IsAllSource)
            return ModelDb.AllRelics.ToList();
        return s.Player.Relics.ToList();
    }

    private static void InvalidateRelicCache(State s) {
        s.CachedAllRelics = GetRelics(s);
    }

    private static void RebuildGrid(State s, string searchText) {
        s.SelectedBg = null;

        foreach (var child in s.RelicGrid.GetChildren()) {
            s.RelicGrid.RemoveChild((Node)child);
            ((Node)child).QueueFree();
        }

        s.FilteredRelics = s.CachedAllRelics.Where(r => {
            if (!MatchesRaritySet(r, s.ActiveRarityFilters)) return false;
            if (!ContentModResolver.MatchesModSourceFilter(
                    ContentModResolver.Resolve(r),
                    s.ActiveModSourceFilters,
                    s.ExcludedModSourceFilters))
                return false;
            if (!string.IsNullOrWhiteSpace(searchText)) {
                var n = GetRelicDisplayName(r);
                var id = GetRelicId(r);
                var combined = n + " " + id;
                if (!combined.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }).ToList();

        s.FilteredRelics.Sort((a, b) => CompareRelics(a, b, s.CurrentSort, s.SortAsc));

        foreach (var relic in s.FilteredRelics) {
            var tile = CreateRelicTile(relic, s.Player, s);
            s.RelicGrid.AddChild(tile);
        }

        Callable.From(() => UpdateGridColumns(s)).CallDeferred();

        s.StatusLabel.Text = string.Format(I18N.T("relicBrowser.count", "{0} / {1} relics"),
            s.FilteredRelics.Count, s.CachedAllRelics.Count);
    }

    private static void UpdateGridColumns(State s) {
        if (!s.RelicGrid.IsNodeReady()) return;
        float w = s.GridScroll.GetRect().Size.X - 2f * GridPadH;
        if (w < 2f) return;
        float slotW = TileMinWidth + GridHSep;
        int cols = Math.Max(1, (int)Math.Floor((w - 4f) / slotW));
        cols = Math.Min(cols, MaxColumns);
        if (s.RelicGrid.Columns != cols)
            s.RelicGrid.Columns = cols;
    }
}
