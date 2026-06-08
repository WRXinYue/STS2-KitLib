using System;
using System.Collections.Generic;
using KitLib.Actions;
using KitLib.AI.Knowledge;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.AI.Combat.Simulation;

internal static class CardPileEffectResolver {
    readonly record struct PileEffects(int Draw, int Discard, int Scry, int ExhaustHand);

    static readonly Dictionary<string, PileEffects> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static int DrawCount(string cardId) => Resolve(cardId).Draw;
    public static int DiscardCount(string cardId) => Resolve(cardId).Discard;
    public static int ScryCount(string cardId) => Resolve(cardId).Scry;
    public static int ExhaustHandCount(string cardId) => Resolve(cardId).ExhaustHand;

    public static (int Draw, int Discard, int Scry) ResolveAll(string cardId) {
        var e = Resolve(cardId);
        return (e.Draw, e.Discard, e.Scry);
    }

    static PileEffects Resolve(string cardId) {
        if (string.IsNullOrWhiteSpace(cardId))
            return default;

        if (Cache.TryGetValue(cardId, out var cached))
            return cached;

        int draw = 0, discard = 0, scry = 0, exhaustHand = 0;
        bool selfExhausts = false;

        foreach (var card in ModelDb.AllCards) {
            if (!string.Equals(card.Id.Entry, cardId, StringComparison.OrdinalIgnoreCase))
                continue;

            selfExhausts = card.Keywords.Contains(CardKeyword.Exhaust);

            foreach (var key in CardEditActions.GetDynamicVarKeys(card)) {
                var amount = CardEditActions.GetDynamicVar(card, key) ?? 0;
                if (amount <= 0) continue;

                var flags = OfficialMechanicProbe.FlagsFromDynamicVar(key);
                if (flags.HasFlag(CardMechanicFlags.HasDraw)
                    && string.Equals(key, "Cards", StringComparison.OrdinalIgnoreCase)) {
                    draw = Math.Max(draw, amount);
                    continue;
                }

                if (flags.HasFlag(CardMechanicFlags.HasDraw))
                    draw = Math.Max(draw, amount);
                if (flags.HasFlag(CardMechanicFlags.HasDiscard))
                    discard = Math.Max(discard, amount);
                if (flags.HasFlag(CardMechanicFlags.HasScry))
                    scry = Math.Max(scry, amount);
            }

            break;
        }

        if (draw == 0 && CardMechanicIndex.TryGet(cardId, out var profile)
            && profile.Flags.HasFlag(CardMechanicFlags.HasDraw))
            draw = 1;

        if (CardMechanicIndex.TryGet(cardId, out var mech)
            && mech.Flags.HasFlag(CardMechanicFlags.HasExhaustFromHand)
            && !selfExhausts)
            exhaustHand = 1;

        var effects = new PileEffects(draw, discard, scry, exhaustHand);
        Cache[cardId] = effects;
        return effects;
    }
}
