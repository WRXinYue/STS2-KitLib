using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using KitLib.Actions;
using KitLib.Modding;
using KitLib.UI.Diagnostics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace KitLib.UI;

internal static partial class CardBrowserUI {
    internal const string GridHolderMetaKey = "kitlib_card_browser_holder";
    internal const float GridHolderDisplayScale = 0.70f;
    internal static readonly Vector2 GridHolderDisplayScaleVector = Vector2.One * GridHolderDisplayScale;
    // Official grid: HoverScale 1.0, SmallScale 0.8 — preserve that ratio for our custom small scale.
    internal static readonly Vector2 GridHolderHoverScaleVector =
        GridHolderDisplayScaleVector * (Vector2.One / NCardHolder.smallScale);

    internal static bool IsBrowserGridHolder(NCardHolder holder) =>
        holder is NGridCardHolder && holder.HasMeta(GridHolderMetaKey);
    private const int CardGridSeparation = 6;
    private static readonly Vector2 BaseCardPixelSize = new(300f, 422f);
    private const int CardBrowserGridPadH = 14;
    private const int CardBrowserGridPadV = 12;
    private const int GridScrollBufferRows = 3;
    private const int GridPreloadBufferRows = 1;
    private const int MaxHoldersPerFrame = 6;

    private static bool _gridLayoutInProgress;

    private static Vector2 CardSize() => BaseCardPixelSize * GridHolderDisplayScale;

    private static float MeasureGridViewportWidth(State s) {
        if (!GodotObject.IsInstanceValid(s.GridScroll))
            return 0f;

        float w = s.GridScroll.GetRect().Size.X;
        if (w < 1f)
            return 0f;

        // Pre-layout scroll rects can report expanded content width; clamp within the main panel only.
        var node = s.GridScroll.GetParent() as Control;
        var mainPanel = s.Dual.MainPanel;
        while (node != null) {
            if (node == mainPanel)
                break;

            var pw = node.GetRect().Size.X;
            if (pw > 1f)
                w = Math.Min(w, pw);
            node = node.GetParent() as Control;
        }

        if (GodotObject.IsInstanceValid(mainPanel)) {
            var panelW = mainPanel.GetRect().Size.X;
            if (panelW > 1f)
                w = Math.Min(w, panelW);
        }

        return w - 2f * CardBrowserGridPadH;
    }

    private static float ContentViewportWidth(State s) {
        var w = MeasureGridViewportWidth(s);
        return w < 2f ? 0f : w;
    }

    private static float GridWidth(State s) {
        var cols = Math.Max(1, s.GridColumns);
        var cardSize = CardSize();
        return cols * cardSize.X + Math.Max(0, cols - 1) * CardGridSeparation;
    }

    private static Vector2 GridOrigin(State s) {
        var viewport = ContentViewportWidth(s);
        if (viewport < 2f)
            return Vector2.Zero;
        // Match NCardGrid: center grid within the scroll viewport, not expanded content width.
        var x = Math.Max(0f, (viewport - GridWidth(s)) * 0.5f);
        return new Vector2(x, 0f);
    }

    private static float RowHeight() => CardSize().Y + CardGridSeparation;

    private static int GetTotalRows(State s) {
        var cols = Math.Max(1, s.GridColumns);
        return s.FilteredCards.Count == 0
            ? 0
            : (int)Math.Ceiling(s.FilteredCards.Count / (double)cols);
    }

    private static int CalculateDisplayedRows(State s) {
        var totalRows = GetTotalRows(s);
        if (totalRows == 0)
            return 0;

        var viewH = s.GridScroll.GetRect().Size.Y;
        if (viewH < 1f)
            viewH = 480f;

        var visibleRows = (int)Math.Ceiling(viewH / RowHeight()) + GridScrollBufferRows + GridPreloadBufferRows;
        return Math.Max(1, Math.Min(totalRows, visibleRows));
    }

    private static int GetPoolSlotCount(State s) =>
        s.CardRows.Sum(row => row.Count);

    private static int CalculateSlidingWindowStart(State s) {
        var cols = Math.Max(1, s.GridColumns);
        if (s.FilteredCards.Count == 0)
            return 0;

        var scrollY = s.GridScroll.ScrollVertical;
        var rowH = RowHeight();
        if (rowH < 1f)
            return s.SlidingWindowStart;

        var topRow = Math.Max(0, (int)((scrollY - CardBrowserGridPadV) / rowH) - GridScrollBufferRows);
        var slotCount = GetPoolSlotCount(s);
        var maxStart = Math.Max(0, s.FilteredCards.Count - slotCount);
        return Math.Min(topRow * cols, maxStart);
    }

    private static void UpdateGridContentSize(State s) {
        var totalRows = GetTotalRows(s);
        var cardSize = CardSize();
        var gridWidth = GridWidth(s);
        var height = totalRows == 0
            ? 0
            : totalRows * cardSize.Y + Math.Max(0, totalRows - 1) * CardGridSeparation;
        s.GridContent.CustomMinimumSize = new Vector2(gridWidth, height);
    }

    private static Vector2 PositionForCardIndex(State s, int cardIndex) {
        var cols = Math.Max(1, s.GridColumns);
        var cardSize = CardSize();
        var col = cardIndex % cols;
        var row = cardIndex / cols;
        // NGridCardHolder positions are card-center anchored (see official NCardGrid.UpdateGridPositions).
        return GridOrigin(s) + cardSize * 0.5f + new Vector2(
            col * (cardSize.X + CardGridSeparation),
            row * (cardSize.Y + CardGridSeparation));
    }

    private static readonly Color ColCardPickNormal = new(0.90f, 0.90f, 0.93f, 1f);
    private static readonly Color ColCardPickSelected = Colors.White;

    private static List<CardModel> GetCards(State s) {
        if (IsLibrarySource)
            return CardLibraryVisibility.GetLibraryCards();
        var t = BrowseSourceToTarget(_browseSource);
        return t.HasValue
            ? CardActions.GetCardsForTarget(s.Player, t.Value)
            : new List<CardModel>();
    }

    private readonly struct GridRebuildOptions {
        public bool RefreshCardList { get; init; }
        public CardModel? InvalidateCard { get; init; }
        public bool InvalidateAllVisuals { get; init; }
        public static GridRebuildOptions LayoutOnly => default;
        public static GridRebuildOptions ForCardEdit(CardModel card) => new() { InvalidateCard = card };
        public static GridRebuildOptions ForCardListChange => new() { RefreshCardList = true };
        public static GridRebuildOptions ForCardListChangeWith(CardModel? card) => new() {
            RefreshCardList = true,
            InvalidateCard = card,
        };
    }

    private static void RefreshCachedCardList(State s) {
        s.CachedAllCards = GetCards(s);
    }

    private static void InvalidateCardCache(State s) {
        var total = CardBrowserPerf.Start();

        ReleaseCardRows(s);
        s.SlidingWindowStart = 0;

        var getCards = CardBrowserPerf.Start();
        s.CachedAllCards = GetCards(s);
        CardBrowserPerf.Log("invalidateCache.getCards", getCards, $"count={s.CachedAllCards.Count}");
        CardBrowserPerf.Log("invalidateCache.total", total);
    }

    private static void ReleaseCardRows(State s) {
        foreach (var row in s.CardRows) {
            foreach (var holder in row)
                holder.QueueFreeSafely();
        }
        s.CardRows.Clear();
        s.SelectedHolder = null;
        s.DisplayedRows = 0;
        s.LastSlidingWindowStart = -1;
        s.GridPopulateScheduled = false;
        s.GridAllocateScheduled = false;
    }

    private static NGridCardHolder? CreateGridHolder(State s, CardModel card) {
        var nCard = NCard.Create(card, ModelVisibility.Visible);
        if (nCard == null)
            return null;

        var holder = NGridCardHolder.Create(nCard);
        if (holder == null) {
            nCard.QueueFreeSafely();
            return null;
        }

        holder.SetMeta(GridHolderMetaKey, true);
        holder.Scale = holder.SmallScale;
        holder.MouseFilter = Control.MouseFilterEnum.Pass;
        holder.Modulate = ColCardPickNormal;
        holder.Pressed += h => OnHolderPressed(s, (NGridCardHolder)h);
        holder.GuiInput += evt => OnHolderGuiInput(s, holder, evt);
        s.GridContent.AddChild(holder);
        nCard.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        ApplyUpgradePreview(s, holder);
        return holder;
    }

    private static int _hoverTipsRestoreZ;
    private static bool _hoverTipsLayerRaised;

    private static void RaiseHoverTipsLayer() {
        if (_hoverTipsLayerRaised)
            return;

        var container = NGame.Instance?.HoverTipsContainer as Control;
        if (container == null)
            return;

        _hoverTipsRestoreZ = container.ZIndex;
        container.ZAsRelative = false;
        container.ZIndex = DevPanelUI.BrowserOverlayZIndex + 50;
        _hoverTipsLayerRaised = true;
    }

    private static void RestoreHoverTipsLayer() {
        if (!_hoverTipsLayerRaised)
            return;

        var container = NGame.Instance?.HoverTipsContainer as Control;
        if (container != null)
            container.ZIndex = _hoverTipsRestoreZ;

        _hoverTipsLayerRaised = false;
    }

    private static void ApplyUpgradePreview(State s, NGridCardHolder holder) {
        if (IsLibrarySource && holder.CardModel.IsUpgradable)
            holder.SetIsPreviewingUpgrade(s.LibraryShowUpgradePreview);
        else
            holder.SetIsPreviewingUpgrade(false);
    }

    private static void OnHolderPressed(State s, NGridCardHolder holder) {
        var card = holder.CardModel;
        if (card == null)
            return;

        if (s.SelectedHolder != null)
            s.SelectedHolder.Modulate = ColCardPickNormal;
        s.SelectedHolder = holder;
        holder.Modulate = ColCardPickSelected;
        ShowRightPanel(s, card);
    }

    private static void OnHolderGuiInput(State s, NGridCardHolder holder, InputEvent evt) {
        if (evt is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
            return;
        if (!mb.DoubleClick || _pickerCallback == null)
            return;

        var card = holder.CardModel;
        if (card == null)
            return;

        _pickerCallback(card.CanonicalInstance);
        holder.AcceptEvent();
    }

    private static void AssignHolderAtIndex(State s, NGridCardHolder holder, int cardIndex, ref int reassigns, ref int skips) {
        if (cardIndex >= s.FilteredCards.Count) {
            holder.Visible = false;
            return;
        }

        var card = s.FilteredCards[cardIndex];
        if (holder.CardModel == card) {
            skips++;
            holder.Visible = true;
            holder.Modulate = s.SelectedHolder == holder ? ColCardPickSelected : ColCardPickNormal;
            return;
        }

        reassigns++;
        holder.ReassignToCard(card, PileType.None, null, ModelVisibility.Visible);
        ApplyUpgradePreview(s, holder);
        holder.Visible = true;
        holder.Modulate = s.SelectedHolder == holder ? ColCardPickSelected : ColCardPickNormal;
    }

    private static void AssignCardsToRow(State s, List<NGridCardHolder> row, int startIndex, ref int reassigns, ref int skips) {
        for (var i = 0; i < row.Count; i++)
            AssignHolderAtIndex(s, row[i], startIndex + i, ref reassigns, ref skips);
    }

    private static NGridCardHolder[] FlattenHolders(State s) {
        var count = GetPoolSlotCount(s);
        if (count == 0)
            return Array.Empty<NGridCardHolder>();

        var slots = new NGridCardHolder[count];
        var idx = 0;
        foreach (var row in s.CardRows) {
            foreach (var holder in row)
                slots[idx++] = holder;
        }
        return slots;
    }

    private static void RebuildCardRows(State s, IReadOnlyList<NGridCardHolder> slots) {
        var cols = Math.Max(1, s.GridColumns);
        s.CardRows.Clear();
        for (var i = 0; i < slots.Count; i += cols) {
            var row = new List<NGridCardHolder>();
            for (var j = 0; j < cols && i + j < slots.Count; j++)
                row.Add(slots[i + j]);
            s.CardRows.Add(row);
        }
    }

    private static void UpdateGridPositions(State s, int startIndex) {
        var index = startIndex;
        foreach (var row in s.CardRows) {
            foreach (var holder in row) {
                if (index < s.FilteredCards.Count) {
                    var pos = PositionForCardIndex(s, index);
                    holder.Position = pos;
                }
                index++;
            }
        }
    }

    private static void InitGrid(State s, bool resetScroll = true) {
        RunGridLayout(s, () => InitGridCore(s, resetScroll));
    }

    private static void InitGridCore(State s, bool resetScroll) {
        if (!s.GridContent.IsNodeReady())
            return;

        TryUpdateCardGridColumns(s);

        var init = CardBrowserPerf.Start();
        ReleaseCardRows(s);
        UpdateGridContentSize(s);

        if (s.FilteredCards.Count == 0) {
            CardBrowserPerf.Log("initGrid", init, "empty");
            return;
        }

        s.DisplayedRows = CalculateDisplayedRows(s);
        s.GridPopulateGeneration++;
        s.GridPopulateCardIdx = 0;
        s.GridPopulateResetScroll = resetScroll;
        s.SlidingWindowStart = 0;
        s.LastSlidingWindowStart = -1;

        var generation = s.GridPopulateGeneration;
        SchedulePopulateGridPoolBatch(s, generation, init);
    }

    private static void SchedulePopulateGridPoolBatch(State s, int generation, Stopwatch? initTimer = null) {
        if (s.GridPopulateScheduled)
            return;

        s.GridPopulateScheduled = true;
        Callable.From(() => {
            s.GridPopulateScheduled = false;
            if (generation != s.GridPopulateGeneration)
                return;

            PopulateGridPoolBatch(s, generation, initTimer);
        }).CallDeferred();
    }

    private static void PopulateGridPoolBatch(State s, int generation, Stopwatch? initTimer) {
        if (generation != s.GridPopulateGeneration || !s.GridContent.IsNodeReady())
            return;

        var cols = Math.Max(1, s.GridColumns);
        var targetCount = Math.Min(s.DisplayedRows * cols, s.FilteredCards.Count);
        var created = 0;

        while (s.GridPopulateCardIdx < targetCount && created < MaxHoldersPerFrame) {
            var rowIdx = s.GridPopulateCardIdx / cols;
            while (s.CardRows.Count <= rowIdx)
                s.CardRows.Add(new List<NGridCardHolder>());

            var holder = CreateGridHolder(s, s.FilteredCards[s.GridPopulateCardIdx]);
            if (holder != null)
                s.CardRows[rowIdx].Add(holder);
            s.GridPopulateCardIdx++;
            created++;
        }

        if (s.CardRows.Count > 0 && s.GridPopulateCardIdx >= cols)
            AllocateCardHoldersCore(s, force: true);

        if (s.GridPopulateCardIdx < targetCount) {
            SchedulePopulateGridPoolBatch(s, generation, initTimer);
            return;
        }

        if (s.GridPopulateResetScroll)
            s.GridScroll.ScrollVertical = 0;

        s.LastSlidingWindowStart = -1;
        s.SlidingWindowStart = 0;
        AllocateCardHoldersCore(s, force: true);

        if (initTimer != null) {
            CardBrowserPerf.Log("initGrid", initTimer,
                $"rows={s.DisplayedRows} cols={cols} pool={GetPoolSlotCount(s)} filtered={s.FilteredCards.Count}");
        }
    }

    private static void RunGridLayout(State s, Action action) {
        if (_gridLayoutInProgress)
            return;

        _gridLayoutInProgress = true;
        try {
            action();
        }
        finally {
            _gridLayoutInProgress = false;
        }
    }

    private static void ScheduleAllocateCardHolders(State s, bool force = false) {
        if (force) {
            s.GridAllocateScheduled = false;
            AllocateCardHolders(s, force: true);
            return;
        }

        if (s.GridAllocateScheduled)
            return;

        s.GridAllocateScheduled = true;
        Callable.From(() => {
            s.GridAllocateScheduled = false;
            AllocateCardHolders(s);
        }).CallDeferred();
    }

    private static void AllocateCardHolders(State s, bool force = false) {
        AllocateCardHoldersCore(s, force);
    }

    private static bool TryRecycleWindowRows(State s, int newStart, int prevStart, int cols,
        ref int reassigns, ref int reassignSkips, out int rowsRecycled) {
        rowsRecycled = 0;
        if (prevStart < 0 || s.CardRows.Count == 0)
            return false;

        var delta = newStart - prevStart;
        if (delta == 0 || cols < 1 || delta % cols != 0)
            return false;

        var rowDelta = delta / cols;
        var rowCount = s.CardRows.Count;
        if (Math.Abs(rowDelta) >= rowCount)
            return false;

        if (rowDelta > 0) {
            for (var i = 0; i < rowDelta; i++) {
                var row = s.CardRows[0];
                s.CardRows.RemoveAt(0);
                s.CardRows.Add(row);
            }

            for (var i = 0; i < rowDelta; i++) {
                var rowIdx = rowCount - rowDelta + i;
                AssignCardsToRow(s, s.CardRows[rowIdx], newStart + rowIdx * cols, ref reassigns, ref reassignSkips);
            }
        }
        else {
            rowDelta = -rowDelta;
            for (var i = 0; i < rowDelta; i++) {
                var row = s.CardRows[^1];
                s.CardRows.RemoveAt(s.CardRows.Count - 1);
                s.CardRows.Insert(0, row);
            }

            for (var i = 0; i < rowDelta; i++)
                AssignCardsToRow(s, s.CardRows[i], newStart + i * cols, ref reassigns, ref reassignSkips);
        }

        rowsRecycled = rowDelta;
        return true;
    }

    private static bool TrySlideWindowSlots(State s, int newStart, int prevStart,
        ref int reassigns, ref int reassignSkips, out int slideDelta) {
        slideDelta = newStart - prevStart;
        if (slideDelta == 0)
            return true;

        var cols = Math.Max(1, s.GridColumns);
        if (slideDelta % cols == 0)
            return false;

        var slots = FlattenHolders(s);
        var slotCount = slots.Length;
        if (slotCount == 0 || Math.Abs(slideDelta) >= slotCount)
            return false;

        var absDelta = Math.Abs(slideDelta);
        var newSlots = new NGridCardHolder[slotCount];

        if (slideDelta > 0) {
            for (var i = 0; i < slotCount - absDelta; i++)
                newSlots[i] = slots[i + absDelta];

            for (var i = slotCount - absDelta; i < slotCount; i++) {
                var holder = slots[i - (slotCount - absDelta)];
                AssignHolderAtIndex(s, holder, newStart + i, ref reassigns, ref reassignSkips);
                newSlots[i] = holder;
            }
        }
        else {
            for (var i = absDelta; i < slotCount; i++)
                newSlots[i] = slots[i - absDelta];

            for (var i = 0; i < absDelta; i++) {
                var holder = slots[slotCount - absDelta + i];
                AssignHolderAtIndex(s, holder, newStart + i, ref reassigns, ref reassignSkips);
                newSlots[i] = holder;
            }
        }

        RebuildCardRows(s, newSlots);
        return true;
    }

    private static void AllocateCardHoldersCore(State s, bool force = false) {
        if (!s.GridContent.IsNodeReady() || s.CardRows.Count == 0)
            return;

        var alloc = CardBrowserPerf.Start();
        var start = CalculateSlidingWindowStart(s);

        if (!force && start == s.SlidingWindowStart) {
            CardBrowserPerf.Log("allocateCardHolders", alloc, "unchanged");
            return;
        }

        UpdateGridContentSize(s);
        if (s.FilteredCards.Count == 0) {
            foreach (var row in s.CardRows) {
                foreach (var holder in row)
                    holder.Visible = false;
            }
            return;
        }

        var cols = Math.Max(1, s.GridColumns);
        var prevStart = s.LastSlidingWindowStart;
        s.SlidingWindowStart = start;

        if (!force && prevStart >= 0) {
            var reassigns = 0;
            var reassignSkips = 0;
            if (TryRecycleWindowRows(s, start, prevStart, cols, ref reassigns, ref reassignSkips, out var rowsRecycled)) {
                s.LastSlidingWindowStart = start;
                UpdateGridPositions(s, start);
                CardBrowserPerf.Log("allocateCardHolders", alloc,
                    $"start={start} recycled={rowsRecycled} reassigns={reassigns}");
                return;
            }

            if (TrySlideWindowSlots(s, start, prevStart, ref reassigns, ref reassignSkips, out var slideDelta)) {
                s.LastSlidingWindowStart = start;
                UpdateGridPositions(s, start);
                CardBrowserPerf.Log("allocateCardHolders", alloc,
                    $"start={start} slid={slideDelta} reassigns={reassigns}");
                return;
            }
        }

        var rowsAssigned = 0;
        var rowsSkipped = 0;
        var fullReassigns = 0;
        var fullReassignSkips = 0;
        for (var rowIdx = 0; rowIdx < s.CardRows.Count; rowIdx++) {
            var rowStart = start + rowIdx * cols;
            if (!force && prevStart >= 0 && rowStart == prevStart + rowIdx * cols) {
                rowsSkipped++;
                continue;
            }

            rowsAssigned++;
            AssignCardsToRow(s, s.CardRows[rowIdx], rowStart, ref fullReassigns, ref fullReassignSkips);
        }
        s.LastSlidingWindowStart = start;
        UpdateGridPositions(s, start);

        CardBrowserPerf.Log("allocateCardHolders", alloc,
            $"start={start} pool={GetPoolSlotCount(s)} reassigns={fullReassigns} rows={rowsAssigned}");
    }

    private static NGridCardHolder? FindHolderForCard(State s, CardModel card) {
        foreach (var row in s.CardRows) {
            foreach (var holder in row) {
                if (holder.CardModel == card)
                    return holder;
            }
        }
        return null;
    }

    private static void InvalidateHostVisual(State s, CardModel card) {
        var holder = FindHolderForCard(s, card);
        if (holder == null)
            return;

        holder.ReassignToCard(card, PileType.None, null, ModelVisibility.Visible);
        ApplyUpgradePreview(s, holder);
    }

    private static void InvalidateAllHostVisuals(State s) {
        foreach (var row in s.CardRows) {
            foreach (var holder in row) {
                if (!holder.Visible)
                    continue;
                var card = holder.CardModel;
                holder.ReassignToCard(card, PileType.None, null, ModelVisibility.Visible);
                ApplyUpgradePreview(s, holder);
            }
        }
    }

    private static void TryHighlightCardHost(State s, CardModel? card) {
        if (card == null)
            return;

        var holder = FindHolderForCard(s, card);
        if (holder == null)
            return;

        if (s.SelectedHolder != null)
            s.SelectedHolder.Modulate = ColCardPickNormal;
        s.SelectedHolder = holder;
        holder.Modulate = ColCardPickSelected;
    }

    private static void OnGridViewportChanged(State s) {
        if (_gridLayoutInProgress)
            return;

        RunGridLayout(s, () => {
            var colsChanged = TryUpdateCardGridColumns(s);
            var neededRows = CalculateDisplayedRows(s);
            if (colsChanged || neededRows != s.DisplayedRows || s.CardRows.Count == 0)
                InitGridCore(s, resetScroll: false);
            else
                AllocateCardHoldersCore(s, force: false);
        });
    }

    private static bool TryUpdateCardGridColumns(State s) {
        if (!s.GridContent.IsNodeReady())
            return false;

        float w = MeasureGridViewportWidth(s);
        var cardSize = CardSize();
        if (w < cardSize.X)
            return false;

        float cellW = cardSize.X + CardGridSeparation;
        int cols = Math.Max(1, (int)((w + CardGridSeparation) / cellW));
        if (s.GridColumns == cols)
            return false;

        s.GridColumns = cols;
        return true;
    }

    private static void RebuildGrid(State s, string searchText, GridRebuildOptions options = default) {
        var total = CardBrowserPerf.Start();
        var preserveSelection = !options.InvalidateAllVisuals && !options.RefreshCardList;
        var selectedCard = preserveSelection ? s.SelectedCard : null;

        if (options.RefreshCardList) {
            var refresh = CardBrowserPerf.Start();
            RefreshCachedCardList(s);
            CardBrowserPerf.Log("rebuildGrid.refreshCardList", refresh, $"count={s.CachedAllCards.Count}");
        }
        if (options.InvalidateCard != null)
            InvalidateHostVisual(s, options.InvalidateCard);
        else if (options.InvalidateAllVisuals)
            InvalidateAllHostVisuals(s);

        if (!preserveSelection)
            s.SelectedHolder = null;

        var filter = CardBrowserPerf.Start();
        s.FilteredCards = s.CachedAllCards.Where(c => {
            if (!MatchesTypeSet(c, s.ActiveTypeFilters)) return false;
            if (IsExcludedByTypeSet(c, s.ExcludedTypeFilters)) return false;
            if (!MatchesRaritySet(c, s.ActiveRarityFilters)) return false;
            if (IsExcludedByRaritySet(c, s.ExcludedRarityFilters)) return false;
            if (!MatchesCostSet(c, s.ActiveCostFilters)) return false;
            if (IsExcludedByCostSet(c, s.ExcludedCostFilters)) return false;
            if (!ContentModResolver.MatchesModSourceFilter(
                    ContentModResolver.Resolve(c),
                    s.ActiveModSourceFilters,
                    s.ExcludedModSourceFilters))
                return false;
            if (IsLibrarySource) {
                if (!MatchesPoolSet(c, s.ActivePoolFilters, s.PoolFilterPredicates)) return false;
                if (IsExcludedByPoolSet(c, s.ExcludedPoolFilters, s.PoolFilterPredicates)) return false;
            }
            if (!string.IsNullOrWhiteSpace(searchText)) {
                var name = CardEditActions.GetCardDisplayName(c);
                var desc = CardPreviewHelper.GetSearchDescription(c);
                var combined = name + " " + desc;
                if (!combined.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }).ToList();
        CardBrowserPerf.Log("rebuildGrid.filter", filter,
            $"in={s.CachedAllCards.Count} out={s.FilteredCards.Count}");

        var sort = CardBrowserPerf.Start();
        s.FilteredCards.Sort((a, b) => CompareCards(a, b, s.SortPriority));
        CardBrowserPerf.Log("rebuildGrid.sort", sort);

        Callable.From(() => InitGrid(s, resetScroll: options.RefreshCardList)).CallDeferred();
        if (selectedCard != null)
            Callable.From(() => TryHighlightCardHost(s, selectedCard)).CallDeferred();

        s.StatusLabel.Text = string.Format(I18N.T("cardBrowser.count", "{0} / {1} cards"),
            s.FilteredCards.Count, s.CachedAllCards.Count);
        CardBrowserPerf.Log("rebuildGrid.total", total,
            $"filtered={s.FilteredCards.Count} cached={s.CachedAllCards.Count}");
    }
}
