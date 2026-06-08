using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.AI.Knowledge;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Random;

namespace KitLib.AI.Combat.Simulation;

internal static class CombatPileSimulator {
    public const int MaxHandSize = 10;
    public const int BaseHandDrawCount = 5;

    static bool _loggedFallbackShuffle;

    public static (List<CombatHandCard> retained, List<CombatPileCard> discard) DiscardHand(
        IReadOnlyList<CombatHandCard> hand,
        IReadOnlyList<CombatPileCard> discard) {
        var retained = new List<CombatHandCard>();
        var newDiscard = discard.ToList();

        foreach (var card in hand) {
            if (card.HasRetain) {
                retained.Add(card);
                continue;
            }

            newDiscard.Add(HandToPile(card));
        }

        return (retained, newDiscard);
    }

    public static (List<CombatPileCard> draw, List<CombatPileCard> discard, int rngCounter) ReshuffleIfNeeded(
        IReadOnlyList<CombatPileCard> draw,
        IReadOnlyList<CombatPileCard> discard,
        uint shuffleSeed,
        int shuffleCounter) {
        var drawPile = draw.ToList();
        var discardPile = discard.ToList();

        if (drawPile.Count > 0 || discardPile.Count == 0)
            return (drawPile, discardPile, shuffleCounter);

        var merged = new List<CombatPileCard>(drawPile);
        merged.AddRange(discardPile);

        if (shuffleSeed == 0) {
            var derived = DeriveFallbackShuffleSeed(merged);
            var fallbackRng = new Rng(derived, shuffleCounter);
            merged.StableShuffle(fallbackRng);
            if (!_loggedFallbackShuffle) {
                _loggedFallbackShuffle = true;
                MainFile.Logger.Warn("[CombatPile] Using hash-derived fallback shuffle (rngShuffle seed missing).");
            }

            return (merged, [], fallbackRng.Counter);
        }

        var rng = new Rng(shuffleSeed, shuffleCounter);
        merged.StableShuffle(rng);
        return (merged, [], rng.Counter);
    }

    public static (List<CombatHandCard> hand, List<CombatPileCard> draw, List<CombatPileCard> discard, int rngCounter)
        DrawHand(
            IReadOnlyList<CombatHandCard> retainedHand,
            IReadOnlyList<CombatPileCard> draw,
            IReadOnlyList<CombatPileCard> discard,
            int count,
            uint shuffleSeed,
            int shuffleCounter) {
        var newHand = retainedHand.Select((c, i) => c with { HandIndex = i }).ToList();
        var drawPile = draw.ToList();
        var discardPile = discard.ToList();
        var counter = shuffleCounter;
        int drawn = 0;

        while (drawn < count && newHand.Count < MaxHandSize) {
            (drawPile, discardPile, counter) = ReshuffleIfNeeded(drawPile, discardPile, shuffleSeed, counter);
            if (drawPile.Count == 0)
                break;

            var card = drawPile[0];
            drawPile.RemoveAt(0);
            newHand.Add(PileToHand(card, newHand.Count));
            drawn++;
        }

        return (newHand, drawPile, discardPile, counter);
    }

    public static (List<CombatHandCard> hand, List<CombatPileCard> draw, List<CombatPileCard> discard, int rngCounter)
        DrawCards(
            IReadOnlyList<CombatHandCard> hand,
            IReadOnlyList<CombatPileCard> draw,
            IReadOnlyList<CombatPileCard> discard,
            int count,
            uint shuffleSeed,
            int shuffleCounter) {
        var slots = MaxHandSize - hand.Count;
        if (slots <= 0 || count <= 0)
            return (hand.ToList(), draw.ToList(), discard.ToList(), shuffleCounter);

        var (newCards, drawAfter, discardAfter, counter) = DrawHand(
            [],
            draw,
            discard,
            Math.Min(count, slots),
            shuffleSeed,
            shuffleCounter);

        var merged = hand.ToList();
        foreach (var card in newCards)
            merged.Add(card with { HandIndex = merged.Count });

        return (merged, drawAfter, discardAfter, counter);
    }

    public static (List<CombatPileCard> pile, int rngCounter) InjectStatusAtRandom(
        IReadOnlyList<CombatPileCard> pile,
        string cardId,
        int count,
        uint shuffleSeed,
        int shuffleCounter) {
        var result = pile.ToList();
        var rng = shuffleSeed == 0
            ? new Rng(DeriveFallbackShuffleSeed(result), shuffleCounter)
            : new Rng(shuffleSeed, shuffleCounter);

        for (int i = 0; i < count; i++) {
            int idx = rng.NextInt(result.Count + 1);
            result.Insert(idx, CreateStatusCard(cardId));
        }

        return (result, rng.Counter);
    }

    public static List<CombatPileCard> InjectStatus(
        IReadOnlyList<CombatPileCard> pile,
        string cardId,
        int count) {
        var result = pile.ToList();
        for (int i = 0; i < count; i++)
            result.Add(CreateStatusCard(cardId));
        return result;
    }

    static CombatPileCard CreateStatusCard(string cardId) =>
        new(
            cardId,
            cardId,
            0,
            0,
            0,
            "Status",
            IsStatus: true,
            HasRetain: false,
            HasExhaust: false);

    public static List<CombatPileCard> AddToBottom(IReadOnlyList<CombatPileCard> pile, CombatPileCard card) {
        var result = pile.ToList();
        result.Add(card);
        return result;
    }

    public static List<CombatPileCard> AddToTop(IReadOnlyList<CombatPileCard> pile, CombatPileCard card) {
        var result = pile.ToList();
        result.Insert(0, card);
        return result;
    }

    public static List<CombatPileCard> RemoveFromPile(IReadOnlyList<CombatPileCard> pile, CombatPileCard card) {
        var result = pile.ToList();
        var idx = result.FindIndex(c => c.Id == card.Id && c.Name == card.Name);
        if (idx >= 0)
            result.RemoveAt(idx);
        return result;
    }

    internal static CombatPileCard HandToPile(CombatHandCard card) =>
        new(
            card.Id,
            card.Name,
            card.Cost,
            card.Damage,
            card.Block,
            card.CardType,
            CombatJunkCard.IsJunkId(card.Id),
            card.HasRetain,
            card.HasExhaust);

    static uint DeriveFallbackShuffleSeed(IReadOnlyList<CombatPileCard> pile) {
        if (pile.Count == 0) return 1;

        var parts = new List<string>(pile.Count);
        foreach (var card in pile)
            parts.Add($"{card.Id}|{card.Name}");
        return (uint)StringHelper.GetDeterministicHashCode(string.Join(";", parts));
    }

    internal static CombatHandCard PileToHand(CombatPileCard card, int index) {
        var json = card.ToJson();
        return new(
            index,
            card.Id,
            card.Name,
            card.Cost,
            card.Damage,
            card.Block,
            card.CardType,
            "",
            CanPlay: !card.IsStatus || card.Damage > 0 || card.Block > 0,
            CardMechanicIndex.InferFromSnapshot(json),
            false,
            card.HasRetain,
            card.HasExhaust,
            CombatCardStats.ResolveHitCount(json));
    }
}
