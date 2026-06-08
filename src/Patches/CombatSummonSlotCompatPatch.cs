using KitLib.Actions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace KitLib.Patches;

/// <summary>
/// Mid-combat adds (including vanilla monster summons like Ovicopter lay eggs) may pass
/// <c>slotName == ""</c> when the current encounter has no slot scene. Treat empty like null
/// and auto-layout enemies, matching <see cref="CombatEnemyActions.AddMonster"/>.
/// </summary>
[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.AddCreature))]
internal static class CombatSummonSlotNormalizePatch
{
    static void Prefix(Creature creature)
    {
        if (string.IsNullOrEmpty(creature.SlotName))
            creature.SlotName = null;
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Add), typeof(Creature))]
internal static class CombatSummonSlotRepositionPatch
{
    static void Postfix(Creature creature)
    {
        if (!creature.IsMonster || creature.PetOwner != null || creature.CombatState == null)
            return;
        if (!string.IsNullOrEmpty(creature.SlotName))
            return;

        if (creature.CombatState is CombatState cs) {
            var id = creature.Monster != null
                ? ((MegaCrit.Sts2.Core.Models.AbstractModel)creature.Monster).Id.Entry
                : "?";
            MainFile.Logger.Info($"[KitLib.CombatAdd] summon reposition hook: {id}");
            CombatEnemyActions.RepositionEnemies(cs);
        }
    }
}
