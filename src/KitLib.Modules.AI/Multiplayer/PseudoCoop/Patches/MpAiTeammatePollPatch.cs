using HarmonyLib;
using KitLib.Singleplayer.Companion;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop.Patches;

[HarmonyPatch(typeof(NRun), nameof(NRun._Process))]
internal static class MpAiTeammatePollPatch {
    static double _accum;
    static double _companionAccum;
    static double _lanLocalAccum;
    static double _spCompanionAccum;
    static double _spCompanionNonCombatAccum;

    [HarmonyPostfix]
    static void Postfix(double delta) {
        MpAiTeammateHost.Poll(delta, ref _accum);
        CompanionDecisionHost.Poll(delta, ref _companionAccum);
        LanLocalDecisionHost.Poll(delta, ref _lanLocalAccum);
        SpvCompanionNonCombatHost.Poll(delta, ref _spCompanionNonCombatAccum);
        SpvCompanionAiHost.Poll(delta, ref _spCompanionAccum);
    }
}
