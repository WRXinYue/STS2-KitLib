using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace DevMode.Patches;

/// <summary>
/// Run player-specific relic setup when dynamic ancient options appear in generated results.
/// </summary>
[HarmonyPatch(typeof(AncientEventModel), "GenerateInitialOptionsWrapper")]
internal static class AncientEventOptionDebugPatch
{
    static void Postfix(AncientEventModel __instance, ref IReadOnlyList<EventOption> __result)
    {
        if (!DevModeState.IsActive || __result.Count == 0)
            return;

        EnsureDynamicRelicSetup(__instance, __result);
    }

    static void EnsureDynamicRelicSetup(AncientEventModel ancient, IReadOnlyList<EventOption> options)
    {
        if (ancient.Owner is null)
            return;

        foreach (var option in options)
        {
            switch (option.Relic)
            {
                case DustyTome tome when tome.AncientCard is null:
                    tome.SetupForPlayer(ancient.Owner);
                    break;
                case TouchOfOrobas touch when touch.StarterRelic is null:
                    touch.SetupForPlayer(ancient.Owner);
                    break;
                case ArchaicTooth tooth when tooth.StarterCard is null:
                    tooth.SetupForPlayer(ancient.Owner);
                    break;
            }
        }
    }
}
