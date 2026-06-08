using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop.Patches;

[HarmonyPatch(typeof(NRun), nameof(NRun._Process))]
internal static class MpAiTeammatePollPatch {
    static double _accum;
    static double _companionAccum;
    static double _lanLocalAccum;

    [HarmonyPostfix]
    static void Postfix(double delta) {
        MpAiTeammateHost.Poll(delta, ref _accum);
        CompanionDecisionHost.Poll(delta, ref _companionAccum);
        LanLocalDecisionHost.Poll(delta, ref _lanLocalAccum);
    }
}
