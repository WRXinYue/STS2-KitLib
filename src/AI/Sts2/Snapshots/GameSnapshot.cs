using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Combat;
using DevMode.AI.Core;
using DevMode.AI.Core.Schema;
using DevMode.AI.Knowledge;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using DevMode.EnemyIntent;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.AI.Sts2.Snapshots;

/// <summary>
/// Captures the full STS2 game state into a <see cref="JsonObject"/>
/// that can be consumed by any <see cref="Core.IDecisionMaker"/>.
/// </summary>
internal static class GameSnapshot
{
    public static JsonObject Capture(RunState state, Player player, GamePhase phase = GamePhase.None)
    {
        var obj = new JsonObject
        {
            ["totalFloor"] = state.TotalFloor,
            ["actIndex"] = state.CurrentActIndex,
            ["actFloor"] = state.ActFloor,
            ["gold"] = player.Gold,
            ["currentHp"] = player.Creature.CurrentHp,
            ["maxHp"] = player.Creature.MaxHp,
            ["characterId"] = player.Character.Id.Entry ?? "",
            ["ascensionLevel"] = state.AscensionLevel,
            ["deck"] = CaptureDeck(player),
            ["relics"] = CaptureRelics(player),
            ["potions"] = CapturePotions(player),
            ["hasOpenPotionSlots"] = player.HasOpenPotionSlots,
            ["potionSlotCount"] = player.PotionSlots.Count,
        };

        var combatState = player.PlayerCombatState;
        if (combatState != null)
            obj["combat"] = CaptureCombat(state, player, combatState);

        var room = state.CurrentRoom;
        if (room != null)
            obj["roomType"] = room.RoomType.ToString();

        GameSnapshotPhaseCapture.Enrich(obj, state, player, phase);
        AiSnapshotHub.Enrich(obj, player, phase);
        return obj;
    }

    /// <summary>Post-action combat fragment for MCP <c>afterState</c> (player powers + enemy HP/powers).</summary>
    internal static JsonObject CaptureCombatAfterState(Player player)
    {
        var obj = new JsonObject
        {
            ["playerPowers"] = player.Creature != null
                ? CapturePowers(player.Creature.Powers)
                : new JsonArray(),
        };

        var cs = CombatManager.Instance?.DebugOnlyGetState();
        if (cs != null)
            obj["enemies"] = CaptureEnemiesBrief(cs);

        return obj;
    }

    private static JsonArray CaptureDeck(Player player)
    {
        var arr = new JsonArray();
        foreach (var c in player.Deck.Cards)
            arr.Add(SnapshotCardJson.FromCard(c));
        return arr;
    }

    private static JsonArray CaptureRelics(Player player)
    {
        var arr = new JsonArray();
        foreach (var r in player.Relics)
        {
            arr.Add(new JsonObject
            {
                ["id"] = SafeRelicId(r),
                ["name"] = r.Title.GetFormattedText(),
                ["rarity"] = SafeRelicRarity(r),
            });
        }
        return arr;
    }

    private static JsonArray CapturePotions(Player player)
    {
        var arr = new JsonArray();
        var slots = player.PotionSlots;
        for (int slot = 0; slot < slots.Count; slot++) {
            var p = slots[slot];
            if (p == null) continue;

            var id = p.Id.Entry ?? "";
            var profile = PotionMechanicIndex.GetOrDefault(id);
            arr.Add(new JsonObject
            {
                ["id"] = id,
                ["slot"] = slot,
                ["category"] = profile.Category.ToString(),
                ["usage"] = profile.Usage,
                ["targetType"] = profile.TargetType,
                ["rarity"] = profile.Rarity,
                ["retainScore"] = PotionTierCatalog.GetRetainScore(id),
            });
        }
        return arr;
    }

    private static JsonObject CaptureCombat(RunState runState, Player player, PlayerCombatState combatState)
    {
        var isPlayPhase = Sts2CombatCompat.IsCombatPlayPhaseActive();
        var cs = CombatManager.Instance?.DebugOnlyGetState();
        var playerBlock = player.Creature?.Block ?? 0;

        var combat = new JsonObject
        {
            ["maxEnergy"] = player.MaxEnergy,
            ["currentEnergy"] = combatState.Energy,
            ["drawPileCount"] = combatState.DrawPile?.Cards.Count() ?? 0,
            ["discardPileCount"] = combatState.DiscardPile?.Cards.Count() ?? 0,
            ["isPlayPhaseActive"] = isPlayPhase,
            ["phase"] = isPlayPhase ? "PlayPhase" : "NotPlayPhase",
            ["playerBlock"] = playerBlock,
            ["turnNumber"] = cs?.RoundNumber ?? 1,
            ["playerPowers"] = player.Creature != null
                ? CapturePowers(player.Creature.Powers)
                : new JsonArray(),
        };

        combat["rngShuffle"] = new JsonObject
        {
            ["seed"] = runState.Rng.Shuffle.Seed,
            ["counter"] = runState.Rng.Shuffle.Counter,
        };

        var hand = new JsonArray();
        if (combatState.Hand?.Cards != null)
        {
            foreach (var c in combatState.Hand.Cards)
            {
                var cardObj = SnapshotCardJson.FromCard(c);
                cardObj["canPlay"] = c.CanPlay(out _, out _);
                hand.Add(cardObj);
            }
        }
        combat["hand"] = hand;
        combat["drawPile"] = CapturePile(combatState.DrawPile);
        combat["discardPile"] = CapturePile(combatState.DiscardPile);
        combat["exhaustPile"] = CapturePile(combatState.ExhaustPile);

        if (cs != null)
            combat["enemies"] = CaptureEnemies(cs, player);

        return combat;
    }

    private static JsonArray CapturePile(CardPile? pile) {
        var arr = new JsonArray();
        if (pile?.Cards == null) return arr;

        foreach (var card in pile.Cards)
            arr.Add(SnapshotCardJson.FromCard(card));

        return arr;
    }

    private static JsonArray CapturePowers(IEnumerable<PowerModel?> powers)
    {
        var arr = new JsonArray();
        foreach (var power in powers)
        {
            if (power == null) continue;

            var entry = new JsonObject
            {
                ["id"] = power.GetType().Name,
                ["amount"] = power.Amount,
            };
            try {
                var modelId = power.Id.Entry;
                if (!string.IsNullOrWhiteSpace(modelId))
                    entry["modelId"] = modelId;
            }
            catch { }

            arr.Add(entry);
        }
        return arr;
    }

    private static JsonArray CaptureEnemies(CombatState cs, Player player)
    {
        var arr = new JsonArray();
        var targets = cs.PlayerCreatures.ToList();
        var index = 0;

        foreach (var enemy in cs.Enemies)
        {
            var obj = new JsonObject
            {
                ["index"] = index++,
                ["currentHp"] = enemy.CurrentHp,
                ["maxHp"] = enemy.MaxHp,
                ["block"] = enemy.Block,
                ["isAlive"] = enemy.IsAlive,
                ["isMinion"] = enemy.IsSecondaryEnemy,
            };

            try {
                var monsterId = enemy.ModelId.Entry;
                if (!string.IsNullOrWhiteSpace(monsterId))
                    obj["monsterId"] = monsterId;
            }
            catch { }

            try {
                var title = enemy.Monster?.Title?.GetFormattedText();
                if (!string.IsNullOrWhiteSpace(title))
                    obj["name"] = title;
            }
            catch { }

            if (enemy.Monster?.NextMove != null)
            {
                obj["nextMoveId"] = enemy.Monster.NextMove.Id;
                var intents = new JsonArray();
                int intentDamage = 0;
                int intentBlock = 0;

                foreach (var intent in enemy.Monster.NextMove.Intents)
                {
                    intents.Add(intent.ToString());
                    if (intent.IntentType == IntentType.Hidden) continue;

                    if (intent is AttackIntent attack)
                    {
                        try {
                            intentDamage += attack.GetTotalDamage(targets, enemy);
                        }
                        catch { }
                    }
                    else if (intent.IntentType == IntentType.Defend)
                    {
                        intentBlock += 5;
                    }
                }

                obj["intents"] = intents;
                obj["intentDamage"] = intentDamage;
                obj["intentBlock"] = intentBlock;

                var intentTags = new JsonArray();
                int nonDamageThreat = 0;
                foreach (var intent in enemy.Monster.NextMove.Intents) {
                    if (intent.IntentType == IntentType.Hidden) continue;
                    intentTags.Add(intent.IntentType.ToString());
                    nonDamageThreat += EnemyThreatWeights.IntentWeight(intent.IntentType);
                }

                obj["intentTags"] = intentTags;
                obj["nonDamageThreat"] = nonDamageThreat;
            }

            try {
                var steps = MonsterIntentReader.CaptureIntentSteps(enemy, targets);
                if (steps.Count > 0)
                    obj["intentSteps"] = steps;
            }
            catch { }

            obj["powers"] = CapturePowers(enemy.Powers);

            var mechanicFlags = EnemyMechanicResolver.ResolveFlags(obj);
            obj["mechanicFlags"] = mechanicFlags.ToString();

            arr.Add(obj);
        }

        CombatEnemyGraph.ObserveAndEnrich(arr);
        return arr;
    }

    private static JsonArray CaptureEnemiesBrief(CombatState cs)
    {
        var arr = new JsonArray();
        var index = 0;
        foreach (var enemy in cs.Enemies)
        {
            arr.Add(new JsonObject
            {
                ["index"] = index++,
                ["currentHp"] = enemy.CurrentHp,
                ["maxHp"] = enemy.MaxHp,
                ["block"] = enemy.Block,
                ["isAlive"] = enemy.IsAlive,
                ["isMinion"] = enemy.IsSecondaryEnemy,
                ["powers"] = CapturePowers(enemy.Powers),
            });
        }
        return arr;
    }

    static string SafeRelicId(RelicModel relic) {
        try { return relic.Id.Entry ?? ""; }
        catch { return ""; }
    }

    static string SafeRelicRarity(RelicModel relic) {
        try { return relic.Rarity.ToString(); }
        catch { return ""; }
    }
}
