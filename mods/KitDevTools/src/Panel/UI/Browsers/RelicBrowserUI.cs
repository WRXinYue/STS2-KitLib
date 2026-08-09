using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using KitLib.Actions;
using KitLib.Icons;
using KitLib.Modding;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.UI;

/// <summary>
/// Self-drawn relic browser — parallel architecture to CardBrowserUI.
/// Center: scrollable grid of compact relic tiles. Right: context-sensitive detail / actions.
/// </summary>
internal static partial class RelicBrowserUI {
    private const string RootName = "KitLibRelicBrowser";
    private const string DualMetaKey = "dm_dual_relic_browser";
    private const string CarrierNodeName = "RelicBrowserDualCarrier";
    private const float DefaultExtWidth = 260f;

    // ──────── Session state ────────

    private sealed class State {
        public readonly NGlobalUi GlobalUi;
        public readonly RunState RunState;
        public readonly Player Player;
        public DevPanelUI.DualColumnOverlayHandle Dual = null!;

        // UI nodes
        public LineEdit SearchInput = null!;
        public ScrollContainer GridScroll = null!;
        public GridContainer RelicGrid = null!;
        public VBoxContainer RightContent = null!;
        public Label StatusLabel = null!;

        // Nav
        public Button[] TabButtons = Array.Empty<Button>();
        public ColorRect Indicator = null!;
        public int ActiveTabIdx;

        // Sort
        public SortField CurrentSort = SortField.Rarity;
        public bool SortAsc = true;
        public Button? SortBtn;

        // Filters
        public readonly HashSet<RelicRarity> ActiveRarityFilters = new();
        public readonly HashSet<string> ActiveModSourceFilters = new();
        public readonly HashSet<string> ExcludedModSourceFilters = new();
        public List<RelicRarity> AvailableRarities = new();

        // Data
        public List<RelicModel> CachedAllRelics = new();
        public List<RelicModel> FilteredRelics = new();

        // Selection
        public RelicModel? SelectedRelic;
        public Panel? SelectedBg;
        public Color SelectedRarityCol;

        public State(NGlobalUi globalUi, RunState runState, Player player) {
            GlobalUi = globalUi;
            RunState = runState;
            Player = player;
        }
    }

    // ──────── Public API ────────

    public static void Show(NGlobalUi globalUi, RunState runState, Player player) {
        Remove(globalUi);

        var s = new State(globalUi, runState, player);

        var dual = DevPanelUI.CreateDualColumnOverlay(new DevPanelUI.DualColumnOverlayOptions {
            GlobalUi = globalUi,
            RootName = RootName,
            DualMetaKey = DualMetaKey,
            CarrierNodeName = CarrierNodeName,
            MainUseMaxWidth = true,
            ExtDefaultWidth = DefaultExtWidth,
            FallbackClose = () => Remove(globalUi),
        });
        s.Dual = dual;
        dual.MainContent.AddThemeConstantOverride("separation", 8);
        var content = dual.MainContent;

        var backBtn = BuildExtensionBackHeader(dual.ExtContent);
        backBtn.Pressed += () => {
            dual.CloseExtension();
            ClearRightPanel(s);
        };

        var extScroll = new ScrollContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        s.RightContent = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        s.RightContent.AddThemeConstantOverride("separation", 10);
        AddPlaceholder(s.RightContent);
        extScroll.AddChild(s.RightContent);
        dual.ExtContent.AddChild(extScroll);

        // ── Nav bar (All / Owned) ──
        var sourceLabels = new[]
        {
            I18N.T("relicBrowser.sourceAll", "All Relics"),
            I18N.T("relicBrowser.sourceOwned", "Owned")
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

        // ── Search bar + sort ──
        var searchRow = new HBoxContainer();
        searchRow.AddThemeConstantOverride("separation", 6);

        searchRow.AddChild(new TextureRect {
            Texture = MdiIcon.Magnify.Texture(18, KitLibTheme.Subtle),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(22, 22),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        });

        s.SearchInput = new LineEdit {
            PlaceholderText = I18N.T("relicBrowser.search", "Search..."),
            ClearButtonEnabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        searchRow.AddChild(s.SearchInput);

        searchRow.AddChild(new Control { CustomMinimumSize = new Vector2(8, 0) });

        s.SortBtn = CreateSortButton(I18N.T("relicBrowser.sortRarity", "Rarity") + " ▲");
        s.SortBtn.Pressed += () => {
            if (s.CurrentSort == SortField.Rarity)
                s.SortAsc = !s.SortAsc;
            else {
                s.CurrentSort = s.CurrentSort == SortField.Alphabet ? SortField.Rarity : SortField.Alphabet;
                s.SortAsc = true;
            }
            RefreshSortButton(s);
            RebuildGrid(s, s.SearchInput.Text ?? "");
        };
        searchRow.AddChild(s.SortBtn);
        content.AddChild(searchRow);
        RefreshSortButton(s);

        // ── Rarity filter chips ──
        InvalidateRelicCache(s);
        s.AvailableRarities = DiscoverRarities(s.CachedAllRelics);

        if (s.AvailableRarities.Count > 0) {
            var chipRow = new HBoxContainer();
            chipRow.AddThemeConstantOverride("separation", 4);

            var filterIcon = new TextureRect {
                Texture = MdiIcon.FilterVariant.Texture(16, KitLibTheme.Subtle),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(18, 18),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
            };
            chipRow.AddChild(filterIcon);

            foreach (var rarity in s.AvailableRarities) {
                var chip = CreateSegmentChip(RarityDisplayName(rarity));
                var captured = rarity;
                chip.Toggled += on => {
                    ToggleSet(s.ActiveRarityFilters, captured, on);
                    RebuildGrid(s, s.SearchInput.Text ?? "");
                };
                chipRow.AddChild(chip);
            }
            content.AddChild(chipRow);
        }

        var relicModSourceRow = BrowserDetailHelpers.TryCreateModSourceFilterRow(
            ContentModResolver.BuildFilterEntries(ModelDb.AllRelics.Cast<AbstractModel>()),
            s.ActiveModSourceFilters,
            s.ExcludedModSourceFilters,
            () => RebuildGrid(s, s.SearchInput.Text ?? ""));
        if (relicModSourceRow != null)
            content.AddChild(relicModSourceRow);

        // ── Spacer between controls and content body ──
        content.AddChild(new Control { CustomMinimumSize = new Vector2(0, 2) });

        // ── Body: relic grid ──
        s.GridScroll = new ScrollContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        s.RelicGrid = new GridContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Columns = 1
        };
        s.RelicGrid.AddThemeConstantOverride("h_separation", GridHSep);
        s.RelicGrid.AddThemeConstantOverride("v_separation", GridVSep);

        var gridPad = new MarginContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        gridPad.AddThemeConstantOverride("margin_left", GridPadH);
        gridPad.AddThemeConstantOverride("margin_right", GridPadH);
        gridPad.AddThemeConstantOverride("margin_top", GridPadV);
        gridPad.AddThemeConstantOverride("margin_bottom", GridPadV);
        gridPad.AddChild(s.RelicGrid);
        s.GridScroll.AddChild(gridPad);
        content.AddChild(s.GridScroll);

        s.GridScroll.Resized += () => UpdateGridColumns(s);
        s.GridScroll.ItemRectChanged += () => UpdateGridColumns(s);

        // ── Status bar ──
        s.StatusLabel = new Label { Text = "" };
        s.StatusLabel.AddThemeFontSizeOverride("font_size", 12);
        s.StatusLabel.AddThemeColorOverride("font_color", ColSubtle);
        content.AddChild(s.StatusLabel);

        // ── Wire up ──
        s.SearchInput.TextChanged += text => RebuildGrid(s, text);

        dual.AttachToScene();

        RebuildGrid(s, "");
        Callable.From(() => UpdateGridColumns(s)).CallDeferred();
    }

    public static void Remove(NGlobalUi globalUi) {
        var parent = (Node)globalUi;
        var node = parent.GetNodeOrNull<Control>(RootName);
        if (node != null) {
            parent.RemoveChild(node);
            node.QueueFree();
        }
    }

    internal static readonly string NodeName = RootName;

    // ──────── Right panel: detail & actions ────────

    private static void ShowRightPanel(State s, RelicModel relic) {
        s.SelectedRelic = relic;
        foreach (var child in s.RightContent.GetChildren()) ((Node)child).QueueFree();
        BuildRelicDetail(s, relic);
        s.Dual.OpenExtension();
    }

    private static void ClearRightPanel(State s) {
        if (s.Dual.ExtSlot.Visible)
            s.Dual.CloseExtension();
        foreach (var child in s.RightContent.GetChildren()) ((Node)child).QueueFree();
        AddPlaceholder(s.RightContent);
        s.SelectedRelic = null;
        s.SelectedBg = null;
    }

    private static Button BuildExtensionBackHeader(VBoxContainer extVbox) {
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
        foreach (var st in new[] { "normal", "hover", "pressed", "focus" })
            backBtn.AddThemeStyleboxOverride(st, flat);
        backBtn.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        backBtn.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(backBtn);
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        extVbox.AddChild(row);
        extVbox.AddChild(new ColorRect {
            CustomMinimumSize = new Vector2(0, 1),
            Color = KitLibTheme.Separator,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        });
        return backBtn;
    }

    private static void BuildRelicDetail(State s, RelicModel relic) {
        var container = s.RightContent;
        var name = GetRelicDisplayName(relic);
        var rarity = GetRelicRarity(relic);
        var rarityCol = RarityToColor(rarity);
        var desc = GetRelicDescription(relic);
        var flavor = GetRelicFlavor(relic);
        var id = GetRelicId(relic);
        bool owned = IsRelicOwned(relic, s.Player);

        // Big icon
        Texture2D? bigIcon = null;
        try { bigIcon = relic.BigIcon ?? relic.Icon; } catch { }
        if (bigIcon != null) {
            var iconRect = new TextureRect {
                Texture = bigIcon,
                CustomMinimumSize = new Vector2(64, 64),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
            };
            container.AddChild(iconRect);
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

        // Rarity badge row
        var metaRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        metaRow.AddThemeConstantOverride("separation", 6);

        if (rarity != RelicRarity.None) {
            var rarityLabel = new Label { Text = RarityDisplayName(rarity) };
            rarityLabel.AddThemeFontSizeOverride("font_size", 11);
            rarityLabel.AddThemeColorOverride("font_color", rarityCol);
            metaRow.AddChild(rarityLabel);
        }

        if (owned) {
            var ownedLabel = new Label { Text = I18N.T("relicBrowser.owned", "Owned") };
            ownedLabel.AddThemeFontSizeOverride("font_size", 11);
            ownedLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.75f, 0.45f));
            metaRow.AddChild(ownedLabel);
        }
        container.AddChild(metaRow);

        // ID
        if (!string.IsNullOrEmpty(id))
            container.AddChild(KitLibTheme.CreateCopyableIdRow(id,
                msg => s.StatusLabel.Text = msg));

        container.AddChild(BrowserDetailHelpers.CreateModSourceRow(ContentModResolver.Resolve(relic)));

        // Description
        if (!string.IsNullOrWhiteSpace(desc)) {
            container.AddChild(new HSeparator());
            var descLabel = KitLibTheme.CreateGameBbcodeLabel();
            descLabel.Text = KitLibTheme.ConvertGameBbcode(desc);
            descLabel.AddThemeFontSizeOverride("normal_font_size", 12);
            descLabel.AddThemeColorOverride("default_color", KitLibTheme.TextSecondary);
            container.AddChild(descLabel);
        }

        // Flavor text
        if (!string.IsNullOrWhiteSpace(flavor) && flavor != desc) {
            var flavorLabel = KitLibTheme.CreateGameBbcodeLabel();
            flavorLabel.Text = KitLibTheme.ConvertGameBbcode(flavor);
            flavorLabel.AddThemeFontSizeOverride("normal_font_size", 11);
            flavorLabel.AddThemeColorOverride("default_color", KitLibTheme.Subtle);
            container.AddChild(flavorLabel);
        }

        container.AddChild(new HSeparator());

        var mpItemSync = MpCheatSession.InMultiplayerRun;
        if (mpItemSync)
            MpCheatUi.AddSessionBanner(container);

        var targetRef = MpCheatUi.TryBuildTargetPlayerPicker(container, s.RunState, s.Player);
        Player TargetPlayer() => targetRef?.Value ?? s.Player;

        // Actions
        if (IsAllSource) {
            var addBtn = CreateActionButton(
                I18N.T("relicBrowser.addRelic", "Add to Inventory"),
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
                    "mpcheat.relicAdd.clientTooltip",
                    "Requests host to sync add relic to your character.");
            }

            addBtn.Pressed += () => {
                async System.Threading.Tasks.Task SyncAddRelicAsync() {
                    var result = MpCheatSession.IsHost
                        ? await MpCheatRelicCoordinator.TryHostAddRelicAsync(TargetPlayer(), relic)
                        : await MpCheatRelicCoordinator.TryClientRequestAddRelicAsync(TargetPlayer(), relic);
                    s.StatusLabel.Text = result;
                    InvalidateRelicCache(s);
                    RebuildGrid(s, s.SearchInput.Text ?? "");
                }

                if (mpItemSync) {
                    s.StatusLabel.Text = MpCheatSession.IsHost
                        ? (MpCheatParticipants.RemotePeerCount > 0
                            ? string.Format(
                                I18N.T("mpcheat.relicAdd.pendingWithPeers", "Syncing add relic… waiting for {0} player(s)."),
                                MpCheatParticipants.RemotePeerCount)
                            : I18N.T("mpcheat.relicAdd.pending", "Syncing add relic to all players…"))
                        : I18N.T("mpcheat.relicAdd.clientPending", "Requesting host to sync add relic…");
                    TaskHelper.RunSafely(SyncAddRelicAsync());
                    return;
                }

                TaskHelper.RunSafely(RelicActions.AddRelic(relic, TargetPlayer()));
                s.StatusLabel.Text = string.Format(I18N.T("relicBrowser.added", "Added: {0}"), name);
            };
            container.AddChild(addBtn);
        }
        else {
            var removeBtn = CreateActionButton(
                I18N.T("relicBrowser.removeRelic", "Remove Relic"),
                new Color(0.65f, 0.25f, 0.25f, 0.9f));
            var relicId = ((AbstractModel)relic).Id.Entry ?? "";
            if (mpItemSync && !MpCheatSession.CanUseMultiplayerCheats) {
                removeBtn.Disabled = true;
                removeBtn.TooltipText = I18N.T(
                    "mpcheat.blocked",
                    "Multiplayer cheat inactive: {0}",
                    MpCheatSession.LastBlockReason ?? "unknown");
            }

            removeBtn.Pressed += () => {
                async System.Threading.Tasks.Task SyncRemoveRelicAsync() {
                    var result = MpCheatSession.IsHost
                        ? await MpCheatRelicCoordinator.TryHostRemoveRelicAsync(TargetPlayer(), relicId)
                        : await MpCheatRelicCoordinator.TryClientRequestRemoveRelicAsync(TargetPlayer(), relicId);
                    s.StatusLabel.Text = result;
                    ClearRightPanel(s);
                    InvalidateRelicCache(s);
                    RebuildGrid(s, s.SearchInput.Text ?? "");
                }

                if (mpItemSync) {
                    s.StatusLabel.Text = MpCheatSession.IsHost
                        ? I18N.T("mpcheat.relicRemove.pending", "Syncing remove relic…")
                        : I18N.T("mpcheat.relicRemove.clientPending", "Requesting host to sync remove relic…");
                    TaskHelper.RunSafely(SyncRemoveRelicAsync());
                    return;
                }

                var ownedRelic = TargetPlayer().Relics.FirstOrDefault(r => r == relic)
                    ?? TargetPlayer().GetRelicById(((AbstractModel)relic).Id);
                if (ownedRelic != null) {
                    TaskHelper.RunSafely(RelicCmd.Remove(ownedRelic));
                    s.StatusLabel.Text = string.Format(I18N.T("relicBrowser.removed", "Removed: {0}"), name);
                    ClearRightPanel(s);
                    InvalidateRelicCache(s);
                    RebuildGrid(s, s.SearchInput.Text ?? "");
                }
            };
            container.AddChild(removeBtn);
        }
    }

    // ──────── Navigation ────────

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
        ClearRightPanel(s);
        InvalidateRelicCache(s);
        RebuildGrid(s, s.SearchInput.Text ?? "");
    }

    private static void RefreshSortButton(State s) {
        if (s.SortBtn == null) return;
        string label = s.CurrentSort switch {
            SortField.Rarity => I18N.T("relicBrowser.sortRarity", "Rarity"),
            SortField.Alphabet => I18N.T("relicBrowser.sortAZ", "A-Z"),
            _ => "?"
        };
        s.SortBtn.Text = label + (s.SortAsc ? " ▲" : " ▼");
        s.SortBtn.AddThemeColorOverride("font_color", ColNavActive);
    }

    private static void AddPlaceholder(VBoxContainer container) {
        var lbl = new Label {
            Text = I18N.T("relicBrowser.selectRelic", "Select a relic"),
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        lbl.AddThemeColorOverride("font_color", ColSubtle);
        container.AddChild(lbl);
    }
}
