using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Actions;
using KitLib.Modding;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace KitLib.UI;

internal static partial class CardBrowserUI {
    // ── Grid constants ──

    private const float CardDisplayScale = 0.65f;
    private const int CardGridSeparation = 6;
    private const float CardSlotInnerPad = 12f;
    private const int CardBrowserGridPadH = 14;
    private const int CardBrowserGridPadV = 12;

    private static readonly Color ColCardPickNormal = new(0.90f, 0.90f, 0.93f, 1f);
    private static readonly Color ColCardPickSelected = Colors.White;

    // ── Primitive helpers ──

    private static Control CreateEmptyHost() {
        float cardW = NCard.defaultSize.X * CardDisplayScale;
        float cardH = NCard.defaultSize.Y * CardDisplayScale;
        float slotW = cardW + 2f * CardSlotInnerPad;
        float slotH = cardH + 2f * CardSlotInnerPad;

        return new Control {
            CustomMinimumSize = new Vector2(slotW, slotH),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Stop,
            FocusMode = Control.FocusModeEnum.None,
            Modulate = ColCardPickNormal
        };
    }

    private static NCard? PopulateHost(State s, Control host, CardModel card) {
        float slotW = host.CustomMinimumSize.X;
        float slotH = host.CustomMinimumSize.Y;
        try {
            var useLibraryUpgradePreview = IsLibrarySource && s.LibraryShowUpgradePreview && card.IsUpgradable;
            CardModel modelForNode;
            try {
                modelForNode = CardPreviewHelper.GetDisplayModel(card, useLibraryUpgradePreview);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[KitLib] Card preview model failed for {card.Id}: {ex.Message}");
                modelForNode = card;
                useLibraryUpgradePreview = false;
            }

            var nCard = NCard.Create(modelForNode);
            if (nCard != null) {
                nCard.Position = new Vector2(slotW / 2f, slotH / 2f);
                nCard.Scale = new Vector2(CardDisplayScale, CardDisplayScale);
                SetDescendantsMouseFilterIgnore(nCard);
                host.AddChild(nCard);
                try {
                    if (useLibraryUpgradePreview)
                        nCard.ShowUpgradePreview();
                    else
                        nCard.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
                }
                catch { }
                return nCard;
            }
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[KitLib] NCard.Create failed for {card.Id}: {ex.Message}");
        }
        AddCardFallback(host, card);
        return null;
    }

    private static void ClearCardGrid(GridContainer grid) {
        foreach (var child in grid.GetChildren()) {
            if (child is Node hostNode) {
                foreach (var sub in hostNode.GetChildren()) {
                    if (sub is NCard)
                        ((Node)sub).QueueFreeSafely();
                }
                hostNode.QueueFree();
            }
        }
    }

    private static void SetDescendantsMouseFilterIgnore(Node root) {
        foreach (var child in root.GetChildren()) {
            if (child is Control c)
                c.MouseFilter = Control.MouseFilterEnum.Ignore;
            SetDescendantsMouseFilterIgnore(child);
        }
    }

    private static void AddCardFallback(Control container, CardModel card) {
        var fallback = new ColorRect {
            Color = new Color(0.2f, 0.2f, 0.25f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        fallback.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        container.AddChild(fallback);

        var label = new Label {
            Text = CardEditActions.GetCardDisplayName(card),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        container.AddChild(label);
    }

    // ── State-aware grid operations ──

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
    }

    private static void ClearHostVisuals(Control host) {
        foreach (var child in host.GetChildren()) {
            if (child is Node node)
                node.QueueFreeSafely();
        }
    }

    private static void InvalidateHostVisual(State s, CardModel card) {
        if (!s.HostCache.TryGetValue(card, out var entry)) return;
        ClearHostVisuals(entry.host);
        s.HostCache[card] = (entry.host, null, false);
    }

    private static void InvalidateAllHostVisuals(State s) {
        foreach (var card in s.HostCache.Keys.ToArray())
            InvalidateHostVisual(s, card);
    }

    private static void RefreshCachedCardList(State s) {
        s.CachedAllCards = GetCards(s);
        var valid = new HashSet<CardModel>(s.CachedAllCards);
        foreach (var card in s.HostCache.Keys.ToArray()) {
            if (valid.Contains(card)) continue;
            var entry = s.HostCache[card];
            ClearHostVisuals(entry.host);
            entry.host.QueueFree();
            s.HostCache.Remove(card);
        }
    }

    private static void InvalidateCardCache(State s) {
        foreach (var child in s.CardGrid.GetChildren())
            s.CardGrid.RemoveChild((Node)child);

        foreach (var (_, entry) in s.HostCache) {
            if (entry.nCard != null)
                ((Node)entry.nCard).QueueFreeSafely();
            entry.host.QueueFree();
        }
        s.HostCache.Clear();
        s.CachedAllCards = GetCards(s);
    }

    private static (Control host, bool isNew) GetOrCreateHost(State s, CardModel card) {
        if (s.HostCache.TryGetValue(card, out var cached))
            return (cached.host, false);

        var host = CreateEmptyHost();
        var capturedCard = card;
        host.GuiInput += evt => {
            if (evt is not InputEventMouseButton mb || !mb.Pressed ||
                mb.ButtonIndex != MouseButton.Left)
                return;
            if (s.SelectedPickHost != null)
                s.SelectedPickHost.Modulate = ColCardPickNormal;
            s.SelectedPickHost = host;
            host.Modulate = ColCardPickSelected;
            host.AcceptEvent();
            ShowRightPanel(s, capturedCard);
        };
        s.HostCache[card] = (host, null, false);
        return (host, true);
    }

    private static void TryHighlightCardHost(State s, CardModel? card) {
        if (card == null) return;
        if (!s.HostCache.TryGetValue(card, out var entry)) return;
        if (s.SelectedPickHost != null)
            s.SelectedPickHost.Modulate = ColCardPickNormal;
        s.SelectedPickHost = entry.host;
        entry.host.Modulate = ColCardPickSelected;
    }

    private static void UpdateCardGridColumns(State s) {
        if (!s.CardGrid.IsNodeReady())
            return;
        float w = s.GridScroll.GetRect().Size.X - 2f * CardBrowserGridPadH;
        if (w < 2f)
            return;
        float scaledCardW = NCard.defaultSize.X * CardDisplayScale;
        float slotW = scaledCardW + 2f * CardSlotInnerPad + CardGridSeparation;
        int cols = Math.Max(1, (int)Math.Floor((w - 4f) / slotW));
        if (s.CardGrid.Columns != cols)
            s.CardGrid.Columns = cols;
    }

    private static void RebuildGrid(State s, string searchText, GridRebuildOptions options = default) {
        var preserveSelection = !options.InvalidateAllVisuals && !options.RefreshCardList;
        var selectedCard = preserveSelection ? s.SelectedCard : null;

        if (options.RefreshCardList)
            RefreshCachedCardList(s);
        if (options.InvalidateCard != null)
            InvalidateHostVisual(s, options.InvalidateCard);
        else if (options.InvalidateAllVisuals)
            InvalidateAllHostVisuals(s);

        if (!preserveSelection)
            s.SelectedPickHost = null;

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

        s.FilteredCards.Sort((a, b) => CompareCards(a, b, s.SortPriority));

        foreach (var child in s.CardGrid.GetChildren())
            s.CardGrid.RemoveChild((Node)child);

        foreach (var card in s.FilteredCards) {
            var (host, _) = GetOrCreateHost(s, card);
            s.CardGrid.AddChild(host);
        }

        Callable.From(() => UpdateCardGridColumns(s)).CallDeferred();
        Callable.From(() => PopulateVisibleHosts(s)).CallDeferred();
        if (selectedCard != null)
            Callable.From(() => TryHighlightCardHost(s, selectedCard)).CallDeferred();

        s.StatusLabel.Text = string.Format(I18N.T("cardBrowser.count", "{0} / {1} cards"),
            s.FilteredCards.Count, s.CachedAllCards.Count);
    }

    private static void PopulateVisibleHosts(State s) {
        if (s.FilteredCards.Count == 0) return;
        int cols = s.CardGrid.Columns;
        if (cols <= 0) cols = 1;

        float slotH = NCard.defaultSize.Y * CardDisplayScale + 2f * CardSlotInnerPad;
        float rowH = slotH + CardGridSeparation;
        float scrollY = s.GridScroll.ScrollVertical;
        float viewH = s.GridScroll.GetRect().Size.Y;
        if (viewH < 1f) return;

        const int buffer = 2;
        int topRow = Math.Max(0, (int)((scrollY - CardBrowserGridPadV) / rowH) - buffer);
        int bottomRow = (int)Math.Ceiling((scrollY + viewH - CardBrowserGridPadV) / rowH) + buffer;

        int startIdx = topRow * cols;
        int endIdx = Math.Min(s.FilteredCards.Count, (bottomRow + 1) * cols);

        for (int i = startIdx; i < endIdx; i++) {
            var card = s.FilteredCards[i];
            if (!s.HostCache.TryGetValue(card, out var entry)) continue;
            if (entry.nCard != null) continue;

            var nCard = PopulateHost(s, entry.host, card);
            s.HostCache[card] = (entry.host, nCard, true);
        }
    }
}
