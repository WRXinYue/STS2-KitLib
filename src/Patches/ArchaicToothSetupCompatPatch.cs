using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Relics;

namespace KitLib.Patches;

/// <summary>
/// Orobas evaluates OptionPool3 before RelicOption assigns Owner; mod Archaic Tooth setups need it bound.
/// </summary>
[HarmonyPatch(typeof(ArchaicTooth), nameof(ArchaicTooth.SetupForPlayer))]
internal static class ArchaicToothSetupCompatPatch
{
    static void Prefix(ArchaicTooth __instance, Player player)
    {
        if (__instance.Owner is null)
            __instance.Owner = player;
    }
}
