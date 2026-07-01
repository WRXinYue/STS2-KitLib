using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using KitLib;
using KitLib.Actions;
using KitLib.Icons;
using KitLib.Modding;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.UI;

/// <summary>
/// Self-drawn card browser replacing official NCardLibrary / NDeckCardSelectScreen.
/// Center: scrollable grid of native NCard thumbnails (padded). Right: context-sensitive operation panel.
/// </summary>
internal static partial class CardBrowserUI {
    private const string RootName = "KitLibCardBrowser";
    private const string DualMetaKey = "dm_dual_card_browser";
    private const string CarrierNodeName = "CardBrowserDualCarrier";
    private const float DefaultExtWidth = 300f;

    private static Color ColSubtle => KitLibTheme.Subtle;

    // ──────── Shared session state ────────

    private sealed class State {
        public readonly NGlobalUi GlobalUi;
        public readonly RunState RunState;
        public readonly Player Player;
        public DevPanelUI.DualColumnOverlayHandle Dual = null!;

        // UI nodes
        public LineEdit SearchInput = null!;
        public ScrollContainer GridScroll = null!;
        public GridContainer CardGrid = null!;
        public VBoxContainer RightContent = null!;
        public Label StatusLabel = null!;

        // Nav bar
        public Button[] TabButtons = Array.Empty<Button>();
        public ColorRect Indicator = null!;
        public readonly BrowseSource[] Sources =
        {
            BrowseSource.AllCards, BrowseSource.Hand, BrowseSource.DrawPile,
            BrowseSource.DiscardPile, BrowseSource.ExhaustPile, BrowseSource.Deck
        };
        public int ActiveTabIdx;

        // Sort
        public Dictionary<SortField, Button> SortBtnMap = new();
        public (SortField field, string label)[] SortFieldDefs = Array.Empty<(SortField, string)>();
        public List<(SortField field, bool asc)> SortPriority => CardBrowserFilterPersistence.SortPriority;

        // Filters (shared across panel opens — see CardBrowserFilterPersistence)
        public HashSet<CardType> ActiveTypeFilters => CardBrowserFilterPersistence.ActiveTypeFilters;
        public HashSet<CardRarity> ActiveRarityFilters => CardBrowserFilterPersistence.ActiveRarityFilters;
        public HashSet<int> ActiveCostFilters => CardBrowserFilterPersistence.ActiveCostFilters;
        public HashSet<string> ActivePoolFilters => CardBrowserFilterPersistence.ActivePoolFilters;
        public HashSet<CardType> ExcludedTypeFilters => CardBrowserFilterPersistence.ExcludedTypeFilters;
        public HashSet<CardRarity> ExcludedRarityFilters => CardBrowserFilterPersistence.ExcludedRarityFilters;
        public HashSet<int> ExcludedCostFilters => CardBrowserFilterPersistence.ExcludedCostFilters;
        public HashSet<string> ExcludedPoolFilters => CardBrowserFilterPersistence.ExcludedPoolFilters;
        public HashSet<string> ActiveModSourceFilters => CardBrowserFilterPersistence.ActiveModSourceFilters;
        public HashSet<string> ExcludedModSourceFilters => CardBrowserFilterPersistence.ExcludedModSourceFilters;
        public readonly Dictionary<string, Func<CardModel, bool>> PoolFilterPredicates = new();

        // UI refs for conditional visibility
        public VBoxContainer PoolFilterSection = null!;
        public HBoxContainer PoolCharacterChipRow = null!;
        public HBoxContainer PoolSpecialChipRow = null!;
        public HBoxContainer LibraryUpgradeRow = null!;

        /// <summary>When true and source is All Cards, thumbnails use the same upgrade preview as official NCardLibrary (MutableClone + UpgradeInternal + ShowUpgradePreview).</summary>
        public bool LibraryShowUpgradePreview {
            get => CardBrowserFilterPersistence.LibraryShowUpgradePreview;
            set => CardBrowserFilterPersistence.LibraryShowUpgradePreview = value;
        }

        // Card data
        public List<CardModel> CachedAllCards = new();
        public Dictionary<CardModel, (Control host, NCard? nCard, bool visualsReady)> HostCache = new();
        public List<CardModel> FilteredCards = new();

        // Selection
        public CardModel? SelectedCard;
        public Control? SelectedPickHost;

        public State(NGlobalUi globalUi, RunState runState, Player player) {
            GlobalUi = globalUi;
            RunState = runState;
            Player = player;
        }
    }

    // ──────── Picker state (set by ShowPicker / Show, cleared on Remove) ────────

    private static Action<CardModel>? _pickerCallback;
    private static Action<VBoxContainer>? _pickerPersistentBuilder;
    // Always points at the current state's FilteredCards so callers can read the live filtered list.
    private static Func<List<CardModel>>? _pickerGetFilteredCards;

    /// <summary>Returns the card list currently visible in the browser after all active filters.</summary>
    internal static IReadOnlyList<CardModel> GetPickerFilteredCards() =>
        _pickerGetFilteredCards?.Invoke() ?? new List<CardModel>();

    // ──────── Public API ────────

    /// <summary>
    /// Opens the card browser in picker mode. Clicking a card in the right panel shows an
    /// "Add to Queue" button that invokes <paramref name="onCardPicked"/> without closing the browser.
    /// <paramref name="buildPersistentContent"/> is appended below the per-card section each time
    /// the right panel is rebuilt; use it to embed persistent queue/action controls.
    /// </summary>
    internal static void ShowPicker(NGlobalUi globalUi, RunState runState, Player player,
        Action<CardModel> onCardPicked, Action<VBoxContainer>? buildPersistentContent = null) {
        // Show calls Remove internally which would clear the callbacks, so set them after.
        Show(globalUi, runState, player);
        _pickerCallback = onCardPicked;
        _pickerPersistentBuilder = buildPersistentContent;
    }

    public static void Show(NGlobalUi globalUi, RunState runState, Player player) {
        Remove(globalUi);

        var s = new State(globalUi, runState, player);
        _pickerGetFilteredCards = () => s.FilteredCards;

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
        s.RightContent = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        s.RightContent.AddThemeConstantOverride("separation", 6);
        AddPlaceholder(s.RightContent);
        extScroll.AddChild(s.RightContent);
        dual.ExtContent.AddChild(extScroll);

        // ── Nav bar ──
        var sourceLabels = new[]
        {
            I18N.T("cardBrowser.sourceAll", "All"),
            I18N.T("topbar.card.hand", "Hand"),
            I18N.T("topbar.card.drawPile", "Draw Pile"),
            I18N.T("topbar.card.discardPile", "Discard"),
            I18N.T("topbar.card.exhaustPile", "Exhaust"),
            I18N.T("topbar.card.deck", "Deck")
        };
        s.TabButtons = new Button[sourceLabels.Length];
        s.ActiveTabIdx = Array.IndexOf(s.Sources, _browseSource);
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
            tab.Pressed += () => SwitchTab(s, idx);
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

        // ── Sort + search bar ──
        var sortRow = new HBoxContainer();
        sortRow.AddThemeConstantOverride("separation", 6);

        sortRow.AddChild(new TextureRect {
            Texture = MdiIcon.FilterVariant.Texture(18, KitLibTheme.Subtle),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(22, 22),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        });

        s.SortFieldDefs = new[]
        {
            (SortField.Type,     I18N.T("cardBrowser.sortType",     "Type")),
            (SortField.Rarity,   I18N.T("cardBrowser.sortRarity",   "Rarity")),
            (SortField.Cost,     I18N.T("cardBrowser.sortCost",     "Cost")),
            (SortField.Alphabet, I18N.T("cardBrowser.sortAlphabet", "A-Z"))
        };

        foreach (var (sf, label) in s.SortFieldDefs) {
            var sortBtn = CreateSortToggleButton(label);
            var capturedField = sf;
            sortBtn.Pressed += () => {
                int idx = s.SortPriority.FindIndex(x => x.field == capturedField);
                if (idx == 0)
                    s.SortPriority[0] = (capturedField, !s.SortPriority[0].asc);
                else {
                    if (idx > 0) s.SortPriority.RemoveAt(idx);
                    s.SortPriority.Insert(0, (capturedField, true));
                }
                RefreshSortButtons(s);
                RebuildGrid(s, s.SearchInput.Text ?? "");
            };
            s.SortBtnMap[sf] = sortBtn;
            sortRow.AddChild(sortBtn);
        }
        RefreshSortButtons(s);

        sortRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        s.SearchInput = new LineEdit {
            PlaceholderText = I18N.T("cardBrowser.search", "Search..."),
            ClearButtonEnabled = true,
            CustomMinimumSize = new Vector2(180, 0),
            Text = CardBrowserFilterPersistence.LastSearchText
        };
        sortRow.AddChild(s.SearchInput);
        content.AddChild(sortRow);

        // ── Filter chips ──
        var chipRow = new HBoxContainer();
        chipRow.AddThemeConstantOverride("separation", 4);

        void AddChipGroup(
            string groupLabel,
            (string text, Action<bool> onInclude, Action<bool> onExclude, bool startInclude, bool startExclude)[] chips) {
            if (chipRow.GetChildCount() > 0) {
                var sep = new VSeparator { CustomMinimumSize = new Vector2(1, 0) };
                sep.AddThemeColorOverride("separator", KitLibTheme.Separator);
                chipRow.AddChild(sep);
            }
            var groupLbl = new Label { Text = groupLabel };
            groupLbl.AddThemeFontSizeOverride("font_size", 11);
            groupLbl.AddThemeColorOverride("font_color", ColSubtle);
            groupLbl.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            chipRow.AddChild(groupLbl);

            foreach (var (text, onInclude, onExclude, startInclude, startExclude) in chips) {
                var initialMode = ResolveFilterChipMode(startInclude, startExclude);
                var chip = CreateFilterChip(text, initialMode == FilterChipMode.Include);
                WireTriStateFilterChip(
                    chip,
                    onInclude,
                    onExclude,
                    initialMode,
                    () => RebuildGrid(s, s.SearchInput.Text ?? ""));
                chipRow.AddChild(chip);
            }
        }

        AddChipGroup(I18N.T("cardBrowser.chipType", "Type"), new (string, Action<bool>, Action<bool>, bool, bool)[]
        {
            (I18N.T("cardBrowser.filterAttack", "Attack"),
                on => ToggleSet(s.ActiveTypeFilters, CardType.Attack, on),
                on => ToggleSet(s.ExcludedTypeFilters, CardType.Attack, on),
                s.ActiveTypeFilters.Contains(CardType.Attack),
                s.ExcludedTypeFilters.Contains(CardType.Attack)),
            (I18N.T("cardBrowser.filterSkill", "Skill"),
                on => ToggleSet(s.ActiveTypeFilters, CardType.Skill, on),
                on => ToggleSet(s.ExcludedTypeFilters, CardType.Skill, on),
                s.ActiveTypeFilters.Contains(CardType.Skill),
                s.ExcludedTypeFilters.Contains(CardType.Skill)),
            (I18N.T("cardBrowser.filterPower", "Power"),
                on => ToggleSet(s.ActiveTypeFilters, CardType.Power, on),
                on => ToggleSet(s.ExcludedTypeFilters, CardType.Power, on),
                s.ActiveTypeFilters.Contains(CardType.Power),
                s.ExcludedTypeFilters.Contains(CardType.Power)),
            (I18N.T("cardBrowser.chipOther", "Other"),
                on => ToggleSet(s.ActiveTypeFilters, CardType.None, on),
                on => ToggleSet(s.ExcludedTypeFilters, CardType.None, on),
                s.ActiveTypeFilters.Contains(CardType.None),
                s.ExcludedTypeFilters.Contains(CardType.None))
        });
        AddChipGroup(I18N.T("cardBrowser.chipRarity", "Rarity"), new (string, Action<bool>, Action<bool>, bool, bool)[]
        {
            (I18N.T("cardBrowser.filterCommon", "Common"),
                on => ToggleSet(s.ActiveRarityFilters, CardRarity.Common, on),
                on => ToggleSet(s.ExcludedRarityFilters, CardRarity.Common, on),
                s.ActiveRarityFilters.Contains(CardRarity.Common),
                s.ExcludedRarityFilters.Contains(CardRarity.Common)),
            (I18N.T("cardBrowser.filterUncommon", "Uncommon"),
                on => ToggleSet(s.ActiveRarityFilters, CardRarity.Uncommon, on),
                on => ToggleSet(s.ExcludedRarityFilters, CardRarity.Uncommon, on),
                s.ActiveRarityFilters.Contains(CardRarity.Uncommon),
                s.ExcludedRarityFilters.Contains(CardRarity.Uncommon)),
            (I18N.T("cardBrowser.filterRare", "Rare"),
                on => ToggleSet(s.ActiveRarityFilters, CardRarity.Rare, on),
                on => ToggleSet(s.ExcludedRarityFilters, CardRarity.Rare, on),
                s.ActiveRarityFilters.Contains(CardRarity.Rare),
                s.ExcludedRarityFilters.Contains(CardRarity.Rare)),
            (I18N.T("cardBrowser.chipOther", "Other"),
                on => ToggleSet(s.ActiveRarityFilters, CardRarity.None, on),
                on => ToggleSet(s.ExcludedRarityFilters, CardRarity.None, on),
                s.ActiveRarityFilters.Contains(CardRarity.None),
                s.ExcludedRarityFilters.Contains(CardRarity.None))
        });
        AddChipGroup(I18N.T("cardBrowser.chipCost", "Cost"), new (string, Action<bool>, Action<bool>, bool, bool)[]
        {
            ("0",
                on => ToggleSet(s.ActiveCostFilters, 0, on),
                on => ToggleSet(s.ExcludedCostFilters, 0, on),
                s.ActiveCostFilters.Contains(0),
                s.ExcludedCostFilters.Contains(0)),
            ("1",
                on => ToggleSet(s.ActiveCostFilters, 1, on),
                on => ToggleSet(s.ExcludedCostFilters, 1, on),
                s.ActiveCostFilters.Contains(1),
                s.ExcludedCostFilters.Contains(1)),
            ("2",
                on => ToggleSet(s.ActiveCostFilters, 2, on),
                on => ToggleSet(s.ExcludedCostFilters, 2, on),
                s.ActiveCostFilters.Contains(2),
                s.ExcludedCostFilters.Contains(2)),
            ("3+",
                on => ToggleSet(s.ActiveCostFilters, 3, on),
                on => ToggleSet(s.ExcludedCostFilters, 3, on),
                s.ActiveCostFilters.Contains(3),
                s.ExcludedCostFilters.Contains(3)),
            ("X",
                on => ToggleSet(s.ActiveCostFilters, CostFilterX, on),
                on => ToggleSet(s.ExcludedCostFilters, CostFilterX, on),
                s.ActiveCostFilters.Contains(CostFilterX),
                s.ExcludedCostFilters.Contains(CostFilterX))
        });
        content.AddChild(chipRow);

        var modSourceRow = BrowserDetailHelpers.TryCreateModSourceFilterRow(
            ContentModResolver.BuildFilterEntries(
                CardLibraryVisibility.GetLibraryCards().Cast<AbstractModel>()),
            s.ActiveModSourceFilters,
            s.ExcludedModSourceFilters,
            () => RebuildGrid(s, s.SearchInput.Text ?? ""));
        if (modSourceRow != null)
            content.AddChild(modSourceRow);

        // ── Pool / character filter chips (AllCards tab only) ──
        s.PoolFilterSection = new VBoxContainer();
        s.PoolFilterSection.AddThemeConstantOverride("separation", 4);
        s.PoolFilterSection.Visible = IsLibrarySource;

        s.PoolCharacterChipRow = new HBoxContainer();
        s.PoolCharacterChipRow.AddThemeConstantOverride("separation", 4);
        s.PoolFilterSection.AddChild(s.PoolCharacterChipRow);

        s.PoolSpecialChipRow = new HBoxContainer();
        s.PoolSpecialChipRow.AddThemeConstantOverride("separation", 4);
        s.PoolFilterSection.AddChild(s.PoolSpecialChipRow);

        // Register predicates for built-in pools
        s.PoolFilterPredicates["ironclad"] = c => c.Pool is IroncladCardPool;
        s.PoolFilterPredicates["silent"] = c => c.Pool is SilentCardPool;
        s.PoolFilterPredicates["defect"] = c => c.Pool is DefectCardPool;
        s.PoolFilterPredicates["regent"] = c => c.Pool is RegentCardPool;
        s.PoolFilterPredicates["necrobinder"] = c => c.Pool is NecrobinderCardPool;
        s.PoolFilterPredicates["colorless"] = c => c.Pool is ColorlessCardPool;
        s.PoolFilterPredicates["ancients"] = c => c.Rarity == CardRarity.Ancient;
        s.PoolFilterPredicates["status"] = c => c.Rarity == CardRarity.Status;
        s.PoolFilterPredicates["curse"] = c => c.Rarity == CardRarity.Curse;
        s.PoolFilterPredicates["event"] = c => c.Rarity == CardRarity.Event;
        s.PoolFilterPredicates["quest"] = c => c.Rarity == CardRarity.Quest;
        s.PoolFilterPredicates["token"] = c => c.Rarity == CardRarity.Token;

        var defaultPoolKey = GetDefaultPoolFilterKeyForPlayer(player);

        void AddPoolChip(HBoxContainer row, string key, string text) {
            var pf = s.ActivePoolFilters;
            var ef = s.ExcludedPoolFilters;
            var startInclude = pf.Contains(key)
                || (pf.Count == 0 && ef.Count == 0 && defaultPoolKey == key);
            var startExclude = ef.Contains(key);
            if (startInclude && !startExclude)
                pf.Add(key);
            var capturedKey = key;
            var initialMode = ResolveFilterChipMode(startInclude && !startExclude, startExclude);
            var chip = CreateFilterChip(text, initialMode == FilterChipMode.Include);
            WireTriStateFilterChip(
                chip,
                on => ToggleSet(s.ActivePoolFilters, capturedKey, on),
                on => ToggleSet(s.ExcludedPoolFilters, capturedKey, on),
                initialMode,
                () => RebuildGrid(s, s.SearchInput.Text ?? ""));
            row.AddChild(chip);
        }

        void AddPoolChipGroupLabel(HBoxContainer row, string label) {
            var lbl = new Label { Text = label };
            lbl.AddThemeFontSizeOverride("font_size", 11);
            lbl.AddThemeColorOverride("font_color", ColSubtle);
            lbl.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(lbl);
        }

        // Character row (playable characters only)
        AddPoolChipGroupLabel(s.PoolCharacterChipRow, I18N.T("cardBrowser.chipCharacter", "Character"));
        AddPoolChip(s.PoolCharacterChipRow, "ironclad", I18N.T("cardBrowser.poolIronclad", "Ironclad"));
        AddPoolChip(s.PoolCharacterChipRow, "silent", I18N.T("cardBrowser.poolSilent", "Silent"));
        AddPoolChip(s.PoolCharacterChipRow, "defect", I18N.T("cardBrowser.poolDefect", "Defect"));
        AddPoolChip(s.PoolCharacterChipRow, "regent", I18N.T("cardBrowser.poolRegent", "Regent"));
        AddPoolChip(s.PoolCharacterChipRow, "necrobinder", I18N.T("cardBrowser.poolNecrobinder", "Necrobinder"));

        // Mod characters: any character whose pool type isn't one of the 5 built-ins
        var builtInPoolTypes = new HashSet<Type>
        {
            typeof(IroncladCardPool), typeof(SilentCardPool), typeof(DefectCardPool),
            typeof(RegentCardPool),   typeof(NecrobinderCardPool)
        };
        var modEntries = new List<(string key, string label)>();
        foreach (var character in ModelDb.AllCharacters) {
            var pool = character.CardPool;
            if (builtInPoolTypes.Contains(pool.GetType())) continue;
            var key = "mod_" + pool.Title;
            var capturedPool = pool;
            s.PoolFilterPredicates[key] = c => c.Pool == capturedPool;
            string label;
            try { label = character.Title.GetFormattedText(); }
            catch { label = pool.Title; }
            modEntries.Add((key, label));
        }
        if (modEntries.Count > 0) {
            var pf = s.ActivePoolFilters;
            var ef = s.ExcludedPoolFilters;
            foreach (var (key, _) in modEntries) {
                if (pf.Contains(key) || (pf.Count == 0 && ef.Count == 0 && defaultPoolKey == key))
                    pf.Add(key);
            }
            s.PoolCharacterChipRow.AddChild(new ModPoolFilterDropdown(
                modEntries,
                s.ActivePoolFilters,
                s.ExcludedPoolFilters,
                () => RebuildGrid(s, s.SearchInput.Text ?? "")));
        }

        s.PoolCharacterChipRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        // Special / non-character pools (Colorless, Ancients, Status, …)
        AddPoolChipGroupLabel(s.PoolSpecialChipRow, I18N.T("cardBrowser.chipSpecial", "Special"));
        AddPoolChip(s.PoolSpecialChipRow, "colorless", I18N.T("cardBrowser.poolColorless", "Colorless"));
        AddPoolChip(s.PoolSpecialChipRow, "ancients", I18N.T("cardBrowser.poolAncients", "Ancients"));
        AddPoolChip(s.PoolSpecialChipRow, "status", I18N.T("cardBrowser.poolStatus", "Status"));
        AddPoolChip(s.PoolSpecialChipRow, "curse", I18N.T("cardBrowser.poolCurse", "Curse"));
        AddPoolChip(s.PoolSpecialChipRow, "event", I18N.T("cardBrowser.poolEvent", "Event"));
        AddPoolChip(s.PoolSpecialChipRow, "quest", I18N.T("cardBrowser.poolQuest", "Quest"));
        AddPoolChip(s.PoolSpecialChipRow, "token", I18N.T("cardBrowser.poolToken", "Token"));
        s.PoolSpecialChipRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        content.AddChild(s.PoolFilterSection);

        // ── Card library only: upgrade preview toggle (same chip style as pool filters) ──
        s.LibraryUpgradeRow = new HBoxContainer();
        s.LibraryUpgradeRow.AddThemeConstantOverride("separation", 6);
        s.LibraryUpgradeRow.Visible = IsLibrarySource;
        var libRowLbl = new Label {
            Text = I18N.T("cardBrowser.chipLibrary", "Library"),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        libRowLbl.AddThemeFontSizeOverride("font_size", 11);
        libRowLbl.AddThemeColorOverride("font_color", ColSubtle);
        s.LibraryUpgradeRow.AddChild(libRowLbl);
        var viewUpgradeChip = CreateFilterChip(
            I18N.T("cardBrowser.viewUpgrades", "View upgrades"),
            CardBrowserFilterPersistence.LibraryShowUpgradePreview);
        Action<bool> applyViewUpgrades = pressed => {
            if (s.LibraryShowUpgradePreview == pressed) return;
            var keepSelection = s.SelectedCard;
            s.LibraryShowUpgradePreview = pressed;
            InvalidateCardCache(s);
            RebuildGrid(s, s.SearchInput.Text ?? "");
            if (keepSelection != null && IsLibrarySource) {
                ShowRightPanel(s, keepSelection);
                Callable.From(() => TryHighlightCardHost(s, keepSelection)).CallDeferred();
            }
            else
                ClearRightPanel(s);
        };
        viewUpgradeChip.Toggled += pressed => applyViewUpgrades(pressed);
        s.LibraryUpgradeRow.AddChild(viewUpgradeChip);
        var showHiddenChip = CreateFilterChip(
            I18N.T("cardBrowser.showHidden", "Show hidden"),
            CardLibraryVisibility.ShowHiddenCards);
        Action<bool> applyShowHidden = pressed => {
            if (CardLibraryVisibility.ShowHiddenCards == pressed) return;
            SettingsStore.SetShowHiddenCards(pressed);
            var keepSelection = s.SelectedCard;
            InvalidateCardCache(s);
            RebuildGrid(s, s.SearchInput.Text ?? "");
            if (keepSelection != null && IsLibrarySource) {
                ShowRightPanel(s, keepSelection);
                Callable.From(() => TryHighlightCardHost(s, keepSelection)).CallDeferred();
            }
            else
                ClearRightPanel(s);
        };
        showHiddenChip.Toggled += pressed => applyShowHidden(pressed);
        s.LibraryUpgradeRow.AddChild(showHiddenChip);
        s.LibraryUpgradeRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        content.AddChild(s.LibraryUpgradeRow);

        // ── Body: card grid ──
        s.GridScroll = new ScrollContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        s.CardGrid = new GridContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Columns = 1
        };
        s.CardGrid.AddThemeConstantOverride("h_separation", CardGridSeparation);
        s.CardGrid.AddThemeConstantOverride("v_separation", CardGridSeparation);

        var gridOuterPad = new MarginContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        gridOuterPad.AddThemeConstantOverride("margin_left", CardBrowserGridPadH);
        gridOuterPad.AddThemeConstantOverride("margin_right", CardBrowserGridPadH);
        gridOuterPad.AddThemeConstantOverride("margin_top", CardBrowserGridPadV);
        gridOuterPad.AddThemeConstantOverride("margin_bottom", CardBrowserGridPadV);
        gridOuterPad.AddChild(s.CardGrid);
        s.GridScroll.AddChild(gridOuterPad);
        content.AddChild(s.GridScroll);

        s.GridScroll.Resized += () => UpdateCardGridColumns(s);
        s.GridScroll.ItemRectChanged += () => UpdateCardGridColumns(s);
        s.GridScroll.GetVScrollBar().ValueChanged += _ => PopulateVisibleHosts(s);

        // ── Status bar ──
        s.StatusLabel = new Label { Text = "" };
        s.StatusLabel.AddThemeFontSizeOverride("font_size", 12);
        s.StatusLabel.AddThemeColorOverride("font_color", ColSubtle);
        content.AddChild(s.StatusLabel);

        // ── Wire up ──
        s.SearchInput.TextChanged += text => {
            CardBrowserFilterPersistence.LastSearchText = text ?? "";
            RebuildGrid(s, text ?? "");
        };

        dual.AttachToScene();

        var initialPileTarget = BrowseSourceToTarget(_browseSource);
        if (initialPileTarget.HasValue)
            KitLibState.CardTarget = initialPileTarget.Value;

        InvalidateCardCache(s);
        RebuildGrid(s, s.SearchInput.Text ?? "");
        Callable.From(() => UpdateCardGridColumns(s)).CallDeferred();
    }

    public static void Remove(NGlobalUi globalUi) {
        _pickerCallback = null;
        _pickerPersistentBuilder = null;
        _pickerGetFilteredCards = null;
        var parent = (Node)globalUi;
        var node = parent.GetNodeOrNull<Control>(RootName);
        if (node != null) {
            parent.RemoveChild(node);
            node.QueueFree();
        }
    }

    internal static readonly string NodeName = RootName;

    // ──────── Navigation / selection helpers ────────

    private static void RebuildGridAndSyncRightPanel(State s, GridRebuildOptions gridOptions) {
        var selected = s.SelectedCard;
        RebuildGrid(s, s.SearchInput.Text ?? "", gridOptions);
        if (selected != null && s.CachedAllCards.Contains(selected))
            ShowRightPanel(s, selected);
        else
            ClearRightPanel(s);
    }

    private static void ShowRightPanel(State s, CardModel card) {
        s.SelectedCard = card;
        foreach (var child in s.RightContent.GetChildren()) ((Node)child).QueueFree();
        var search = () => s.SearchInput.Text ?? "";
        CardBrowserRightPanel.Build(s.RightContent, s.StatusLabel, card, s.RunState, s.Player, s.GlobalUi,
            () => RebuildGrid(s, search(), GridRebuildOptions.ForCardEdit(card)),
            () => RebuildGridAndSyncRightPanel(s, GridRebuildOptions.ForCardListChangeWith(card)),
            IsLibrarySource, BrowseSourceToTarget(_browseSource),
            IsLibrarySource && s.LibraryShowUpgradePreview,
            _pickerCallback, _pickerPersistentBuilder);
        s.Dual.OpenExtension();
    }

    private static void ClearRightPanel(State s) {
        if (s.Dual.ExtSlot.Visible)
            s.Dual.CloseExtension();
        foreach (var child in s.RightContent.GetChildren()) ((Node)child).QueueFree();
        AddPlaceholder(s.RightContent);
        s.SelectedCard = null;
        s.SelectedPickHost = null;
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

    private static void SwitchTab(State s, int tabIdx) {
        if (tabIdx == s.ActiveTabIdx) return;
        s.ActiveTabIdx = tabIdx;
        _browseSource = s.Sources[tabIdx];

        for (int i = 0; i < s.TabButtons.Length; i++) {
            bool a = i == tabIdx;
            s.TabButtons[i].AddThemeColorOverride("font_color", a ? ColNavActive : ColNavInactive);
            s.TabButtons[i].AddThemeColorOverride("font_hover_color", a ? ColNavActive : ColNavHover);
            s.TabButtons[i].AddThemeColorOverride("font_pressed_color", ColNavActive);
        }

        MoveIndicator(s, tabIdx, true);
        s.PoolFilterSection.Visible = IsLibrarySource;
        s.LibraryUpgradeRow.Visible = IsLibrarySource;
        var pileTarget = BrowseSourceToTarget(_browseSource);
        if (pileTarget.HasValue)
            KitLibState.CardTarget = pileTarget.Value;
        ClearRightPanel(s);
        InvalidateCardCache(s);
        RebuildGrid(s, s.SearchInput.Text ?? "");
    }

    private static void RefreshSortButtons(State s) {
        var primary = s.SortPriority.Count > 0 ? s.SortPriority[0] : ((SortField?)null, true);
        foreach (var (sf, btn) in s.SortBtnMap) {
            bool isPrimary = primary.Item1.HasValue && primary.Item1.Value == sf;
            string arrow = isPrimary ? (s.SortPriority[0].asc ? " ▲" : " ▼") : "";
            string baseText = s.SortFieldDefs.First(x => x.field == sf).label;
            btn.Text = baseText + arrow;

            var col = isPrimary ? ColNavActive : ColNavInactive;
            btn.AddThemeColorOverride("font_color", col);
            btn.AddThemeColorOverride("font_hover_color", isPrimary ? ColNavActive : ColNavHover);
        }
    }

    private static void AddPlaceholder(VBoxContainer container) {
        var lbl = new Label {
            Text = I18N.T("cardBrowser.selectCard", "Select a card"),
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        lbl.AddThemeColorOverride("font_color", ColSubtle);
        container.AddChild(lbl);
    }
}
