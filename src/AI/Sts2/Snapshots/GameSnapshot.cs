using System.Linq;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Combat;
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
    public static JsonObject Capture(RunState state, Player player)
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
            ["deck"] = CaptureDeck(player),
            ["relics"] = CaptureRelics(player),
            ["potions"] = CapturePotions(player),
        };

        var combatState = player.PlayerCombatState;
        if (combatState != null)
            obj["combat"] = CaptureCombat(player, combatState);

        var room = state.CurrentRoom;
        if (room != null)
            obj["roomType"] = room.RoomType.ToString();

        return obj;
    }

    private static JsonArray CaptureDeck(Player player)
    {
        var arr = new JsonArray();
        foreach (var c in player.Deck.Cards)
        {
            arr.Add(new JsonObject
            {
                ["id"] = c.Id.Entry,
                ["name"] = c.Title,
                ["cost"] = c.EnergyCost.Canonical,
                ["upgradeLevel"] = c.CurrentUpgradeLevel,
                ["maxUpgradeLevel"] = c.MaxUpgradeLevel,
                ["cardType"] = c.Type.ToString(),
            });
        }
        return arr;
    }

    private static JsonArray CaptureRelics(Player player)
    {
        var arr = new JsonArray();
        foreach (var r in player.Relics)
            arr.Add(JsonValue.Create(r.Title.GetFormattedText()));
        return arr;
    }

    private static JsonArray CapturePotions(Player player)
    {
        var arr = new JsonArray();
        foreach (var p in player.Potions)
        {
            arr.Add(new JsonObject
            {
                ["id"] = p.Id.Entry,
                ["targetType"] = p.TargetType.ToString(),
            });
        }
        return arr;
    }

    private static JsonObject CaptureCombat(Player player, PlayerCombatState combatState)
    {
        var combat = new JsonObject
        {
            ["maxEnergy"] = player.MaxEnergy,
            ["currentEnergy"] = combatState.Energy,
            ["drawPileCount"] = combatState.DrawPile?.Cards.Count() ?? 0,
            ["discardPileCount"] = combatState.DiscardPile?.Cards.Count() ?? 0,
        };

        // Hand
        var hand = new JsonArray();
        if (combatState.Hand?.Cards != null)
        {
            foreach (var c in combatState.Hand.Cards)
            {
                var cardObj = new JsonObject
                {
                    ["id"] = c.Id.Entry,
                    ["name"] = c.Title,
                    ["cost"] = c.EnergyCost.Canonical,
                    ["cardType"] = c.Type.ToString(),
                    ["targetType"] = c.TargetType.ToString(),
                };
                cardObj["canPlay"] = c.CanPlay(out _, out _);
                hand.Add(cardObj);
            }
        }
        combat["hand"] = hand;

        // Enemies
        var combatManager = CombatManager.Instance;
        var cs = combatManager?.DebugOnlyGetState();
        if (cs != null)
            combat["enemies"] = CaptureEnemies(cs);

        return combat;
    }

    private static JsonArray CaptureEnemies(CombatState cs)
    {
        var arr = new JsonArray();
        foreach (var enemy in cs.Enemies)
        {
            var obj = new JsonObject
            {
                ["currentHp"] = enemy.CurrentHp,
                ["maxHp"] = enemy.MaxHp,
                ["block"] = enemy.Block,
                ["isAlive"] = enemy.IsAlive,
            };

            if (enemy.Monster?.NextMove != null)
            {
                obj["nextMoveId"] = enemy.Monster.NextMove.Id;
                var intents = new JsonArray();
                foreach (var intent in enemy.Monster.NextMove.Intents)
                    intents.Add(intent.ToString());
                obj["intents"] = intents;
            }

            // Powers / buffs
            var powers = new JsonArray();
            foreach (var power in enemy.Powers)
            {
                powers.Add(new JsonObject
                {
                    ["id"] = power.GetType().Name,
                    ["amount"] = power.Amount,
                });
            }
            obj["powers"] = powers;

            arr.Add(obj);
        }
        return arr;
    }
}
