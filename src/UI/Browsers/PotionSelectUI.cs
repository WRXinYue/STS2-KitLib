using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Actions;
using KitLib.Hooks;
using KitLib.Icons;
using KitLib.Modding;
using KitLib.Multiplayer.Cheat;
using KitLib.Settings;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.UI;

/// <summary>Potion browser — same two-pane layout as RelicBrowserUI.</summary>
internal static class PotionSelectUI {
    private const string RootName = "KitLibPotionBrowser";
    private const float RightPanelW = 240f;
    private const float RailLeft = 24f;
    private const float RailW = 52f;
    private const float PanelLeft = RailLeft + RailW;
    private const float PanelRight = 24f;
    private const int RailRadius = 14;

    // ── Grid tile constants (parallel to RelicBrowserUI.Grid.cs) ────────
    private const float TileMinWidth = 90f;
    private const float IconFrameSize = 80f;
    private const float IconPad = 10f;
    private const float IconSize = IconFrameSize - IconPad * 2f;
    private const int FrameRadius = 18;
    private const int GridHSep = 10;
    private const int GridVSep = 16;
    private const int GridPadH = 14;
    private const int GridPadV = 12;
    private const int MaxColumns = 7;

    private static readonly Color ColFrameBg = new(0.13f, 0.13f, 0.17f, 0.70f);
    private static readonly Color ColFrameHover = new(0.17f, 0.17f, 0.22f, 0.85f);
    private static readonly Color ColFrameSelected = new(0.18f, 0.22f, 0.30f, 0.92f);
    private const float BorderAlphaRest = 0.22f;
    private const float BorderAlphaHover = 0.55f;
    private const float BorderAlphaSelected = 0.88f;

    private enum BrowseSource { All, Owned }
    private static BrowseSource _browseSource = BrowseSource.All;
    private static bool IsAllSource => _browseSource == BrowseSource.All;

    // ── Colours ──────────────────────────────────────────────────────────

    private static Color ColNavActive => KitLibTheme.Accent;
    private static Color ColNavAccent => KitLibTheme.AccentAlpha;
    private static Color ColPanelBg => KitLibTheme.PanelBg;
    private static Color ColPanelBorder => KitLibTheme.PanelBorder;
    private static Color ColSubtle => KitLibTheme.Subtle;
    private static Color ColNavInactive => KitLibTheme.Subtle;
    private static Color ColNavHover => KitLibTheme.TextPrimary;
    private static Color ColSegOff => KitLibTheme.ButtonBgNormal;
    private static Color ColSegHover => KitLibTheme.ButtonBgHover;
    private static readonly Color ColSegOn = new(0.25f, 0.40f, 0.65f, 0.90f);
    private static readonly Color ColSegOnHover = new(0.30f, 0.48f, 0.75f, 0.95f);

    // ── Session state ─────────────────────────────────────────────────────

    private sealed class State {
        public readonly NGlobalUi GlobalUi;
        public readonly Player Player;

        public LineEdit SearchInput = null!;
        public ScrollContainer GridScroll = null!;
        public GridContainer PotionGrid = null!;
        public VBoxContainer RightContent = null!;
        public Label StatusLabel = null!;

        public Button[] TabButtons = Array.Empty<Button>();
        public ColorRect Indicator = null!;
        public int ActiveTabIdx;

        public readonly HashSet<PotionRarity> ActiveRarityFilters = new();
        public readonly HashSet<string> ActiveModSourceFilters = new();
        public readonly HashSet<string> ExcludedModSourceFilters = new();
        public List<PotionRarity> AvailableRarities = new();
        public List<PotionModel> CachedPotions = new();

        public PotionModel? SelectedPotion;
        public Panel? SelectedFrame;
        public Color SelectedRarityCol;

        public State(NGlobalUi globalUi, Player player) {
            GlobalUi = globalUi;
            Player = player;
        }
    }

    // ── Public API ───────────────────────────────────────────────────────

    public static void Show(NGlobalUi globalUi, Player player) {
        Remove(globalUi);

        var s = new State(globalUi, player);

        var (root, _, content) = DevPanelUI.CreateBrowserOverlayShell(
            globalUi, RootName, CreateBrowserPanel(), () => Remove(globalUi), contentSeparation: 8);

        // ── Nav tabs ──
        var sourceLabels = new[]
        {
            I18N.T("potionBrowser.sourceAll",   "All Potions"),
            I18N.T("potionBrowser.sourceOwned", "Owned")
        };
        var sources = new[] { BrowseSource.All, BrowseSource.Owned };

        s.TabButtons = new Button[sourceLabels.Length];
        s.ActiveTabIdx = Array.IndexOf(sources, _browseSource);
        if (s.ActiveTabIdx < 0) s.ActiveTabIdx = 0;

        var navSection = new Control {
            CustomMinimumSize = new Vector2(0, 34),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        var tabRow = new HBoxContainer();
        tabRow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        tabRow.AddThemeConstantOverride("separation", 0);

        s.Indicator = new ColorRect {
            Color = ColNavAccent,
            AnchorLeft = 0,
            AnchorRight = 0,
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetTop = -2,
            OffsetBottom = 0
        };

        for (int i = 0; i < sourceLabels.Length; i++) {
            int idx = i;
            var tab = CreateNavTab(sourceLabels[idx], idx == s.ActiveTabIdx);
            tab.Pressed += () => SwitchTab(s, sources, idx);
            s.TabButtons[idx] = tab;
            tabRow.AddChild(tab);
        }
        tabRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        navSection.AddChild(tabRow);
        navSection.AddChild(s.Indicator);
        navSection.Ready += () =>
            Callable.From(() => MoveIndicator(s, s.ActiveTabIdx, false)).CallDeferred();

        var navOuter = new VBoxContainer();
        navOuter.AddThemeConstantOverride("separation", 0);
        navOuter.AddChild(navSection);
        navOuter.AddChild(new ColorRect {
            CustomMinimumSize = new Vector2(0, 1),
            Color = KitLibTheme.Separator,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        });
        content.AddChild(navOuter);

        // ── Search bar ──
        var searchRow = new HBoxContainer();
        searchRow.AddThemeConstantOverride("separation", 6);
        searchRow.AddChild(new TextureRect {
            Texture = MdiIcon.Magnify.Texture(18, KitLibTheme.Subtle),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(22, 22),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        });
        s.SearchInput = new LineEdit {
            PlaceholderText = I18N.T("potionBrowser.search", "Search potions..."),
            ClearButtonEnabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        searchRow.AddChild(s.SearchInput);
        content.AddChild(searchRow);

        // ── Rarity filter chips ──
        InvalidateCache(s);
        s.AvailableRarities = DiscoverRarities(s.CachedPotions);

        if (s.AvailableRarities.Count > 0) {
            var chipRow = new HBoxContainer();
            chipRow.AddThemeConstantOverride("separation", 4);
            chipRow.AddChild(new TextureRect {
                Texture = MdiIcon.FilterVariant.Texture(16, KitLibTheme.Subtle),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(18, 18),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
            });
            foreach (var rarity in s.AvailableRarities) {
                var chip = CreateSegmentChip(RarityDisplayName(rarity));
                var cap = rarity;
                chip.Toggled += on => {
                    if (on) s.ActiveRarityFilters.Add(cap);
                    else s.ActiveRarityFilters.Remove(cap);
                    RebuildGrid(s, s.SearchInput.Text ?? "");
                };
                chipRow.AddChild(chip);
            }
            content.AddChild(chipRow);
        }

        var potionModSourceRow = BrowserDetailHelpers.TryCreateModSourceFilterRow(
            ContentModResolver.BuildFilterEntries(ModelDb.AllPotions.Cast<AbstractModel>()),
            s.ActiveModSourceFilters,
            s.ExcludedModSourceFilters,
            () => RebuildGrid(s, s.SearchInput.Text ?? ""));
        if (potionModSourceRow != null)
            content.AddChild(potionModSourceRow);

        content.AddChild(new Control { CustomMinimumSize = new Vector2(0, 2) });

        // ── Body: grid (left) + detail (right) ──
        var body = new HSplitContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        body.DraggerVisibility = SplitContainer.DraggerVisibilityEnum.Hidden;
        content.AddChild(body);

        // Left: scrollable grid
        s.GridScroll = new ScrollContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        s.PotionGrid = new GridContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Columns = 1
        };
        s.PotionGrid.AddThemeConstantOverride("h_separation", GridHSep);
        s.PotionGrid.AddThemeConstantOverride("v_separation", GridVSep);

        var gridPad = new MarginContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        gridPad.AddThemeConstantOverride("margin_left", GridPadH);
        gridPad.AddThemeConstantOverride("margin_right", GridPadH);
        gridPad.AddThemeConstantOverride("margin_top", GridPadV);
        gridPad.AddThemeConstantOverride("margin_bottom", GridPadV);
        gridPad.AddChild(s.PotionGrid);
        s.GridScroll.AddChild(gridPad);
        body.AddChild(s.GridScroll);

        s.GridScroll.Resized += () => UpdateGridColumns(s);
        s.GridScroll.ItemRectChanged += () => UpdateGridColumns(s);

        // Right: detail panel
        var rightPanel = new PanelContainer {
            CustomMinimumSize = new Vector2(RightPanelW, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        var rightStyle = new StyleBoxFlat {
            BgColor = ColPanelBg,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 14,
            ContentMarginBottom = 14,
            BorderWidthLeft = 1,
            BorderColor = ColPanelBorder
        };
        rightPanel.AddThemeStyleboxOverride("panel", rightStyle);

        var rightScroll = new ScrollContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        s.RightContent = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        s.RightContent.AddThemeConstantOverride("separation", 10);
        AddPlaceholder(s.RightContent);

        rightScroll.AddChild(s.RightContent);
        rightPanel.AddChild(rightScroll);
        body.AddChild(rightPanel);

        // ── Status bar ──
        s.StatusLabel = new Label { Text = "" };
        s.StatusLabel.AddThemeFontSizeOverride("font_size", 12);
        s.StatusLabel.AddThemeColorOverride("font_color", ColSubtle);
        content.AddChild(s.StatusLabel);

        // ── Wire up ──
        s.SearchInput.TextChanged += text => RebuildGrid(s, text);
        ((Node)globalUi).AddChild(root);
        RebuildGrid(s, "");
        Callable.From(() => UpdateGridColumns(s)).CallDeferred();
        s.SearchInput.GrabFocus();
    }

    public static void Remove(NGlobalUi globalUi) {
        var parent = (Node)globalUi;
        var node = parent.GetNodeOrNull<Control>(RootName);
        if (node == null) return;
        parent.RemoveChild(node);
        node.QueueFree();
    }

    // ── Icon tile (parallel to RelicBrowserUI.Grid.cs: CreateRelicTile) ─

    private static Control CreatePotionTile(PotionModel potion, State s) {
        var rarity = potion.Rarity;
        var rarityCol = RarityToColor(rarity);
        var name = PotionActions.GetPotionDisplayName(potion);

        var outer = new VBoxContainer {
            CustomMinimumSize = new Vector2(TileMinWidth, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = name
        };
        outer.AddThemeConstantOverride("separation", 6);

        // ── Icon frame ──
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

        // Potion image
        Texture2D? iconTex = null;
        try { iconTex = potion.Image; } catch { }

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
            // Fallback: rarity-tinted colour block
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

        // Owned badge (green dot, bottom-right corner of frame)
        if (IsAllSource && s.Player.Potions.Any(p => p.CanonicalInstance == potion)) {
            var badge = new Panel {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorLeft = 1,
                AnchorRight = 1,
                AnchorTop = 1,
                AnchorBottom = 1,
                OffsetLeft = -16,
                OffsetRight = -4,
                OffsetTop = -16,
                OffsetBottom = -4
            };
            var badgeStyle = new StyleBoxFlat {
                BgColor = new Color(0.28f, 0.72f, 0.42f, 0.92f),
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6,
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

        // ── Name label ──
        var nameColor = rarity == PotionRarity.None
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
            if (s.SelectedPotion != potion)
                SetFrameStyle(frame, ColFrameHover, rarityCol, BorderAlphaHover);
        };
        outer.MouseExited += () => {
            if (s.SelectedPotion != potion)
                SetFrameStyle(frame, ColFrameBg, rarityCol, BorderAlphaRest);
        };
        outer.GuiInput += evt => {
            if (evt is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
                return;
            SelectTile(s, frame, potion, rarityCol);
            outer.AcceptEvent();
        };

        return outer;
    }

    // ── Frame / selection helpers ────────────────────────────────────────

    private static Color RarityBorderColor(Color rarity, float alpha)
        => new(rarity.R, rarity.G, rarity.B, alpha);

    private static void SetFrameStyle(Panel frame, Color bg, Color rarityCol, float borderAlpha) {
        if (frame.GetThemeStylebox("panel") is StyleBoxFlat sb) {
            sb.BgColor = bg;
            sb.BorderColor = RarityBorderColor(rarityCol, borderAlpha);
        }
    }

    private static void SelectTile(State s, Panel frame, PotionModel potion, Color rarityCol) {
        if (s.SelectedFrame != null)
            SetFrameStyle(s.SelectedFrame, ColFrameBg, s.SelectedRarityCol, BorderAlphaRest);

        s.SelectedFrame = frame;
        s.SelectedPotion = potion;
        s.SelectedRarityCol = rarityCol;
        SetFrameStyle(frame, ColFrameSelected, rarityCol, BorderAlphaSelected);
        ShowDetail(s, potion);
    }

    // ── Grid rebuild ─────────────────────────────────────────────────────

    private static void InvalidateCache(State s) {
        s.CachedPotions = IsAllSource
            ? PotionActions.GetAllPotions()
                .OrderBy(PotionActions.GetPotionDisplayName)
                .ToList()
            : s.Player.Potions
                .Select(p => p.CanonicalInstance)
                .OrderBy(PotionActions.GetPotionDisplayName)
                .ToList();
    }

    private static void RebuildGrid(State s, string filter) {
        s.SelectedFrame = null;

        foreach (var child in s.PotionGrid.GetChildren()) {
            s.PotionGrid.RemoveChild((Node)child);
            ((Node)child).QueueFree();
        }

        IEnumerable<PotionModel> items = s.CachedPotions;
        if (s.ActiveRarityFilters.Count > 0)
            items = items.Where(p => s.ActiveRarityFilters.Contains(p.Rarity));
        items = items.Where(p => ContentModResolver.MatchesModSourceFilter(
            ContentModResolver.Resolve(p),
            s.ActiveModSourceFilters,
            s.ExcludedModSourceFilters));
        if (!string.IsNullOrWhiteSpace(filter))
            items = items.Where(p => PotionActions.GetPotionDisplayName(p)
                .Contains(filter, StringComparison.OrdinalIgnoreCase));

        var list = items.ToList();

        foreach (var potion in list)
            s.PotionGrid.AddChild(CreatePotionTile(potion, s));

        Callable.From(() => UpdateGridColumns(s)).CallDeferred();

        s.StatusLabel.Text = I18N.T("potionBrowser.count", "{0} potions", list.Count);
    }

    private static void UpdateGridColumns(State s) {
        if (!s.PotionGrid.IsNodeReady()) return;
        float w = s.GridScroll.GetRect().Size.X - 2f * GridPadH;
        if (w < 2f) return;
        int cols = Math.Max(1, (int)Math.Floor((w - 4f) / (TileMinWidth + GridHSep)));
        cols = Math.Min(cols, MaxColumns);
        if (s.PotionGrid.Columns != cols)
            s.PotionGrid.Columns = cols;
    }

    // ── Right panel ──────────────────────────────────────────────────────

    private static void ShowDetail(State s, PotionModel potion) {
        s.SelectedPotion = potion;
        foreach (var child in s.RightContent.GetChildren()) ((Node)child).QueueFree();
        BuildDetail(s, potion);
    }

    private static void ClearDetail(State s) {
        foreach (var child in s.RightContent.GetChildren()) ((Node)child).QueueFree();
        AddPlaceholder(s.RightContent);
        s.SelectedPotion = null;
        s.SelectedFrame = null;
    }

    private static void BuildDetail(State s, PotionModel potion) {
        var container = s.RightContent;
        var name = PotionActions.GetPotionDisplayName(potion);
        var rarity = potion.Rarity;
        var rarityCol = RarityToColor(rarity);

        // Icon (larger version in detail)
        Texture2D? icon = null;
        try { icon = potion.Image; } catch { }
        if (icon != null) {
            container.AddChild(new TextureRect {
                Texture = icon,
                CustomMinimumSize = new Vector2(80, 80),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
            });
        }

        // Name
        var nameLabel = new Label {
            Text = name,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 15);
        container.AddChild(nameLabel);

        // Rarity + owned badges
        var metaRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        metaRow.AddThemeConstantOverride("separation", 6);
        if (rarity != PotionRarity.None) {
            var rarityLabel = new Label { Text = RarityDisplayName(rarity) };
            rarityLabel.AddThemeFontSizeOverride("font_size", 11);
            rarityLabel.AddThemeColorOverride("font_color", rarityCol);
            metaRow.AddChild(rarityLabel);
        }
        if (s.Player.Potions.Any(p => p.CanonicalInstance == potion)) {
            var ownedLabel = new Label { Text = I18N.T("potionBrowser.owned", "Owned") };
            ownedLabel.AddThemeFontSizeOverride("font_size", 11);
            ownedLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.75f, 0.45f));
            metaRow.AddChild(ownedLabel);
        }
        if (metaRow.GetChildCount() > 0) container.AddChild(metaRow);

        // ID
        var id = ((AbstractModel)potion).Id.Entry;
        if (!string.IsNullOrEmpty(id))
            container.AddChild(KitLibTheme.CreateCopyableIdRow(id,
                msg => s.StatusLabel.Text = msg));

        container.AddChild(BrowserDetailHelpers.CreateModSourceRow(ContentModResolver.Resolve(potion)));

        // Description
        string? desc = PotionActions.GetPotionDescriptionFormatted(potion);
        if (!string.IsNullOrWhiteSpace(desc)) {
            container.AddChild(new HSeparator());
            var descLabel = KitLibTheme.CreateGameBbcodeLabel();
            descLabel.Text = KitLibTheme.ConvertGameBbcode(desc);
            descLabel.AddThemeFontSizeOverride("normal_font_size", 12);
            descLabel.AddThemeColorOverride("default_color", KitLibTheme.TextSecondary);
            container.AddChild(descLabel);
        }

        container.AddChild(new HSeparator());

        var mpItemSync = MpCheatSession.InMultiplayerRun;
        if (mpItemSync)
            MpCheatUi.AddSessionBanner(container);

        MpCheatTargetPlayerRef? targetRef = null;
        if (IsAllSource) {
            var runState = RunManager.Instance?.DebugOnlyGetState();
            if (runState != null)
                targetRef = MpCheatUi.TryBuildTargetPlayerPicker(container, runState, s.Player);
        }
        Player TargetPlayer() => targetRef?.Value ?? s.Player;

        // Action button
        if (IsAllSource) {
            var addBtn = CreateActionButton(
                I18N.T("potionBrowser.add", "Add to Inventory"),
                new Color(0.25f, 0.55f, 0.35f, 0.9f));
            if (mpItemSync && !MpCheatSession.CanUseMultiplayerCheats) {
                addBtn.Disabled = true;
                addBtn.TooltipText = I18N.T(
                    "mpcheat.blocked",
                    "Multiplayer cheat inactive: {0}",
                    MpCheatSession.LastBlockReason ?? "unknown");
            }
            else if (mpItemSync && !MpCheatSession.IsHost) {
                addBtn.TooltipText = I18N.T(
                    "mpcheat.potionAdd.clientTooltip",
                    "Requests host to sync add potion to your character.");
            }

            addBtn.Pressed += () => {
                async System.Threading.Tasks.Task SyncAddPotionAsync() {
                    var result = MpCheatSession.IsHost
                        ? await MpCheatPotionCoordinator.TryHostAddPotionAsync(TargetPlayer(), potion)
                        : await MpCheatPotionCoordinator.TryClientRequestAddPotionAsync(TargetPlayer(), potion);
                    s.StatusLabel.Text = result;
                    InvalidateCache(s);
                    RebuildGrid(s, s.SearchInput.Text ?? "");
                }

                if (mpItemSync) {
                    s.StatusLabel.Text = MpCheatSession.IsHost
                        ? I18N.T("mpcheat.potionAdd.pending", "Syncing add potion…")
                        : I18N.T("mpcheat.potionAdd.clientPending", "Requesting host to sync add potion…");
                    TaskHelper.RunSafely(SyncAddPotionAsync());
                    return;
                }

                TaskHelper.RunSafely(PotionActions.AddPotion(TargetPlayer(), potion));
                s.StatusLabel.Text = string.Format(I18N.T("potionBrowser.added", "Added: {0}"), name);
            };
            container.AddChild(addBtn);

            var autoApplyBtn = CreateActionButton(
                I18N.T("potionBrowser.autoApply", "Add to Auto-Apply"),
                new Color(0.25f, 0.55f, 0.38f, 0.85f));
            autoApplyBtn.Pressed += () => {
                var potionId = ((AbstractModel)potion).Id.Entry;
                var entry = new HookEntry {
                    Name = name,
                    Trigger = TriggerType.CombatStart,
                    Actions = [new HookAction
                    {
                        Type     = ActionType.UsePotion,
                        TargetId = potionId,
                    }],
                };
                SettingsStore.Current.Hooks.Add(entry);
                SettingsStore.Save();
                s.StatusLabel.Text = string.Format(
                    I18N.T("potionBrowser.autoApplyAdded", "Auto-apply added: {0}"), name);
            };
            container.AddChild(autoApplyBtn);
        }
        else {
            var ownedPotion = s.Player.Potions.FirstOrDefault(p => p.CanonicalInstance == potion);
            if (ownedPotion != null) {
                var slotIndex = PotionActions.GetPotionSlotIndex(s.Player, ownedPotion);
                var discardBtn = CreateActionButton(
                    I18N.T("potionBrowser.discard", "Discard Potion"),
                    new Color(0.65f, 0.25f, 0.25f, 0.9f));
                if (mpItemSync && !MpCheatSession.CanUseMultiplayerCheats) {
                    discardBtn.Disabled = true;
                    discardBtn.TooltipText = I18N.T(
                        "mpcheat.blocked",
                        "Multiplayer cheat inactive: {0}",
                        MpCheatSession.LastBlockReason ?? "unknown");
                }

                discardBtn.Pressed += () => {
                    async System.Threading.Tasks.Task SyncDiscardAsync() {
                        var result = MpCheatSession.IsHost
                            ? await MpCheatPotionCoordinator.TryHostDiscardPotionAsync(s.Player, slotIndex)
                            : await MpCheatPotionCoordinator.TryClientRequestDiscardPotionAsync(s.Player, slotIndex);
                        s.StatusLabel.Text = result;
                        ClearDetail(s);
                        InvalidateCache(s);
                        RebuildGrid(s, s.SearchInput.Text ?? "");
                    }

                    if (mpItemSync) {
                        s.StatusLabel.Text = MpCheatSession.IsHost
                            ? I18N.T("mpcheat.potionRemove.pending", "Syncing discard potion…")
                            : I18N.T("mpcheat.potionRemove.clientPending", "Requesting host to sync discard potion…");
                        TaskHelper.RunSafely(SyncDiscardAsync());
                        return;
                    }

                    TaskHelper.RunSafely(PotionActions.DiscardPotion(ownedPotion));
                    s.StatusLabel.Text = string.Format(I18N.T("potionBrowser.discarded", "Discarded: {0}"), name);
                    ClearDetail(s);
                    InvalidateCache(s);
                    RebuildGrid(s, s.SearchInput.Text ?? "");
                };
                container.AddChild(discardBtn);
            }
        }
    }

    // ── Navigation ───────────────────────────────────────────────────────

    private static void MoveIndicator(State s, int tabIdx, bool animate) {
        if (tabIdx < 0 || tabIdx >= s.TabButtons.Length) return;
        var btn = s.TabButtons[tabIdx];
        float left = btn.Position.X;
        float right = left + btn.Size.X;

        if (animate && s.Indicator.IsInsideTree()) {
            var tween = s.Indicator.CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(s.Indicator, "offset_left", left, 0.25f)
                 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(s.Indicator, "offset_right", right, 0.25f)
                 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        else {
            s.Indicator.OffsetLeft = left;
            s.Indicator.OffsetRight = right;
        }
    }

    private static void SwitchTab(State s, BrowseSource[] sources, int tabIdx) {
        if (tabIdx == s.ActiveTabIdx) return;
        s.ActiveTabIdx = tabIdx;
        _browseSource = sources[tabIdx];

        for (int i = 0; i < s.TabButtons.Length; i++) {
            bool a = i == tabIdx;
            s.TabButtons[i].AddThemeColorOverride("font_color", a ? ColNavActive : ColNavInactive);
            s.TabButtons[i].AddThemeColorOverride("font_hover_color", a ? ColNavActive : ColNavHover);
            s.TabButtons[i].AddThemeColorOverride("font_pressed_color", ColNavActive);
        }

        MoveIndicator(s, tabIdx, true);
        ClearDetail(s);
        InvalidateCache(s);
        RebuildGrid(s, s.SearchInput.Text ?? "");
    }

    // ── Widget factories ─────────────────────────────────────────────────

    private static Button CreateNavTab(string text, bool active) {
        var btn = new Button {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(0, 32)
        };
        var flat = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 4,
            ContentMarginBottom = 6
        };
        foreach (var st in new[] { "normal", "hover", "pressed", "focus" })
            btn.AddThemeStyleboxOverride(st, flat);
        btn.AddThemeColorOverride("font_color", active ? ColNavActive : ColNavInactive);
        btn.AddThemeColorOverride("font_hover_color", active ? ColNavActive : ColNavHover);
        btn.AddThemeColorOverride("font_pressed_color", ColNavActive);
        btn.AddThemeFontSizeOverride("font_size", 13);
        return btn;
    }

    private static Button CreateSegmentChip(string text) {
        var btn = new Button {
            Text = text,
            ToggleMode = true,
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(0, 26)
        };
        StyleBoxFlat MakeStyle(Color bg) => new() {
            BgColor = bg,
            CornerRadiusTopLeft = 13,
            CornerRadiusTopRight = 13,
            CornerRadiusBottomLeft = 13,
            CornerRadiusBottomRight = 13,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 2,
            ContentMarginBottom = 2
        };
        btn.AddThemeStyleboxOverride("normal", MakeStyle(ColSegOff));
        btn.AddThemeStyleboxOverride("hover", MakeStyle(ColSegHover));
        btn.AddThemeStyleboxOverride("pressed", MakeStyle(ColSegOn));
        btn.AddThemeStyleboxOverride("hover_pressed", MakeStyle(ColSegOnHover));
        btn.AddThemeStyleboxOverride("focus", MakeStyle(ColSegOff));
        btn.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        btn.AddThemeColorOverride("font_hover_color", KitLibTheme.TextPrimary);
        btn.AddThemeColorOverride("font_pressed_color", KitLibTheme.TextPrimary);
        btn.AddThemeFontSizeOverride("font_size", 11);
        return btn;
    }

    private static Button CreateActionButton(string text, Color bgColor) {
        var btn = new Button {
            Text = text,
            CustomMinimumSize = new Vector2(0, 36),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        StyleBoxFlat MakeStyle(Color bg) => new() {
            BgColor = bg,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 5,
            ContentMarginBottom = 5
        };
        btn.AddThemeStyleboxOverride("normal", MakeStyle(bgColor));
        btn.AddThemeStyleboxOverride("hover", MakeStyle(bgColor.Lightened(0.15f)));
        btn.AddThemeStyleboxOverride("pressed", MakeStyle(bgColor.Lightened(0.15f)));
        btn.AddThemeStyleboxOverride("focus", MakeStyle(bgColor));
        btn.AddThemeFontSizeOverride("font_size", 13);
        return btn;
    }

    private static PanelContainer CreateBrowserPanel() {
        var panel = new PanelContainer {
            Name = "BrowserPanel",
            MouseFilter = Control.MouseFilterEnum.Stop,
            AnchorLeft = 0,
            AnchorRight = 1,
            OffsetLeft = PanelLeft,
            OffsetRight = -PanelRight,
            AnchorTop = 0.15f,
            AnchorBottom = 0.85f
        };
        var style = new StyleBoxFlat {
            BgColor = ColPanelBg,
            CornerRadiusTopLeft = 0,
            CornerRadiusBottomLeft = 0,
            CornerRadiusTopRight = RailRadius,
            CornerRadiusBottomRight = RailRadius,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 12,
            ContentMarginBottom = 16,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthRight = 1,
            BorderColor = ColPanelBorder,
            ShadowColor = new Color(0, 0, 0, 0.40f),
            ShadowSize = 20,
            ShadowOffset = new Vector2(20, 0)
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var content = new VBoxContainer {
            Name = "Content",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 10);
        panel.AddChild(content);
        return panel;
    }

    private static void AddPlaceholder(VBoxContainer container) {
        var lbl = new Label {
            Text = I18N.T("potionBrowser.hint", "Select a potion"),
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        lbl.AddThemeColorOverride("font_color", ColSubtle);
        container.AddChild(lbl);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static List<PotionRarity> DiscoverRarities(List<PotionModel> potions) =>
        potions
            .Select(p => p.Rarity)
            .Where(r => r is PotionRarity.Common or PotionRarity.Uncommon or PotionRarity.Rare)
            .Distinct()
            .OrderBy(r => r)
            .ToList();

    private static string RarityDisplayName(PotionRarity rarity) => rarity switch {
        PotionRarity.Common => I18N.T("rarity.common", "Common"),
        PotionRarity.Uncommon => I18N.T("rarity.uncommon", "Uncommon"),
        PotionRarity.Rare => I18N.T("rarity.rare", "Rare"),
        PotionRarity.Event => I18N.T("rarity.event", "Event"),
        PotionRarity.Token => I18N.T("rarity.token", "Token"),
        _ => rarity.ToString()
    };

    private static Color RarityToColor(PotionRarity rarity) => rarity switch {
        PotionRarity.Common => KitLibTheme.RarityCommon,
        PotionRarity.Uncommon => KitLibTheme.RarityUncommon,
        PotionRarity.Rare => KitLibTheme.RarityRare,
        PotionRarity.Event => KitLibTheme.RaritySpecial,
        PotionRarity.Token => KitLibTheme.Subtle,
        _ => ColSubtle
    };
}
