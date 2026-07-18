using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.Singleplayer.Companion;

internal static class SpvCompanionCombatJoin {
    static readonly AccessTools.FieldRef<NCombatRoom, ICombatRoomVisuals> VisualsRef =
        AccessTools.FieldRefAccess<NCombatRoom, ICombatRoomVisuals>("_visuals");

    internal static void TryJoinActiveCombat(Player companion) {
        var cm = CombatManager.Instance;
        if (cm?.IsInProgress != true)
            return;

        TaskHelper.RunSafely(JoinAsync(companion));
    }

    static async Task JoinAsync(Player companion) {
        var cm = CombatManager.Instance!;
        var combat = cm.DebugOnlyGetState();
        if (combat == null)
            return;

        if (combat.Players.Any(p => p.NetId == companion.NetId))
            return;

        SpvCompanionSession.MarkCombatBootstrapPending(companion.NetId);
        try {
            companion.ResetCombatState();
            companion.PopulateCombatState(companion.RunState.Rng.Shuffle, combat);
            combat.AddPlayer(companion);

            var creature = companion.Creature;
            cm.AddCreature(creature);
            NCombatRoom.Instance?.AddCreature(creature);
            await cm.AfterCreatureAdded(creature);
            await SpvCompanionCombatTurnBootstrap.TryBootstrapMidCombatTurnAsync(companion);
            Callable.From(RepositionCombatAllies).CallDeferred();

            KitLog.Info("SpCompanion", $"Joined active combat netId={companion.NetId}.");
        }
        catch (System.Exception ex) {
            KitLog.Warn("SpCompanion", $"Failed to join active combat netId={companion.NetId}: {ex.Message}");
        }
        finally {
            SpvCompanionSession.MarkCombatBootstrapComplete(companion.NetId);
        }
    }

    static void RepositionCombatAllies() {
        var room = NCombatRoom.Instance;
        if (room == null)
            return;

        var visuals = VisualsRef(room);
        if (visuals == null)
            return;

        var allyNodes = room.CreatureNodes
            .Where(n => n.Entity.IsPlayer || n.Entity.PetOwner != null)
            .ToList();
        if (allyNodes.Count == 0)
            return;

        NCombatRoom.PositionPlayersAndPets(
            allyNodes,
            visuals.Encounter.GetCameraScaling(),
            visuals.Encounter.FullyCenterPlayers);
    }
}
