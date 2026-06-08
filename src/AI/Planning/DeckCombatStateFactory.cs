using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Knowledge;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Random;

namespace KitLib.AI.Planning;

/// <summary>Builds turn-1 combat states from deck snapshots for macro evaluation.</summary>
public static class DeckCombatStateFactory {
    public static CombatState BuildOpeningTurn(
        JsonObject snapshot,
        JsonObject? offeredCard,
        IReadOnlyList<CombatEnemy> enemies,
        int sampleIndex) {
        var deck = BuildDeckArray(snapshot, offeredCard);
        var pile = BuildDrawPile(deck);
        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;
        var shuffleSeed = DeriveShuffleSeed(deck, floor, sampleIndex);

        var shuffled = pile.ToList();
        var rng = new Rng(shuffleSeed, 0);
        shuffled.StableShuffle(rng);
        int shuffleCounter = rng.Counter;

        var drawPile = shuffled;
        var discard = new List<CombatPileCard>();
        var retained = new List<CombatHandCard>();

        (var hand, drawPile, discard, shuffleCounter) = CombatPileSimulator.DrawHand(
            retained,
            drawPile,
            discard,
            CombatPileSimulator.BaseHandDrawCount,
            shuffleSeed,
            shuffleCounter);

        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 60;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 60;
        var maxEnergy = snapshot["combat"]?.AsObject()?["maxEnergy"]?.GetValue<int>() ?? 3;
        var relicIds = RelicCombatRules.ParseRelicIds(snapshot);
        var modifiers = RelicCombatRules.MergeModifiers(relicIds, []);
        var block = RelicCombatRules.StartOfCombatBlock(relicIds);

        return new CombatState(
            hp,
            maxHp,
            block,
            maxEnergy,
            maxEnergy,
            0,
            1,
            hand,
            drawPile,
            discard,
            [],
            modifiers,
            enemies.ToList(),
            relicIds,
            [],
            false,
            shuffleSeed,
            shuffleCounter);
    }

    static JsonArray BuildDeckArray(JsonObject snapshot, JsonObject? offeredCard) {
        var deck = snapshot["deck"]?.AsArray() ?? new JsonArray();
        var result = new JsonArray();
        foreach (var node in deck) {
            if (node != null)
                result.Add(node.DeepClone());
        }

        if (offeredCard != null)
            result.Add(offeredCard.DeepClone());

        return result;
    }

    static List<CombatPileCard> BuildDrawPile(JsonArray deck) {
        var pile = new List<CombatPileCard>();
        foreach (var node in deck) {
            if (node is JsonObject card)
                pile.Add(CombatPileCard.FromJson(card));
        }

        return pile;
    }

    static uint DeriveShuffleSeed(JsonArray deck, int floor, int sampleIndex) {
        var parts = new List<string> { $"f{floor}", $"s{sampleIndex}" };
        foreach (var node in deck) {
            if (node is JsonObject card)
                parts.Add(card["id"]?.GetValue<string>() ?? "?");
        }

        var hash = StringHelper.GetDeterministicHashCode(string.Join("|", parts));
        return hash == 0 ? 1u : (uint)hash;
    }
}
