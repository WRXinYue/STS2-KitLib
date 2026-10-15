using HarmonyLib;
using KitLib;
using KitLib.Actions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace KitLib.Patches;

/// <summary>
/// Mid-combat adds may pass <c>slotName == ""</c>; auto-layout enemies when empty.
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Add), typeof(Creature))]
internal static class CombatSummonSlotRepositionPatch {
    static void Postfix(Creature creature) {
        if (!creature.IsMonster || creature.PetOwner != null || creature.CombatState == null)
            return;
        if (!string.IsNullOrEmpty(creature.SlotName))
            return;

        if (creature.CombatState is CombatState cs) {
            var id = creature.Monster != null
                ? ((MegaCrit.Sts2.Core.Models.AbstractModel)creature.Monster).Id.Entry
                : "?";
            KitLog.Info("CombatAdd", $"summon reposition hook: {id}");
            CombatEnemyActions.RepositionEnemies(cs);
        }
    }
}
