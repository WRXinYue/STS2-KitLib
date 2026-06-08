using System.Linq;
using KitLib.AI.Sts2.Snapshots;
using KitLib.Multiplayer.SyncBot;
using MegaCrit.Sts2.Core.Entities.Models;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>Heuristic remote choices for simulated peers (replaces fixed index 0 when teammate AI is on).</summary>
internal static class MpChoiceBot {
    public static NetPlayerChoiceResult Decide(Player player) {
        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null)
            return MpCheatSyncBot.DefaultIndexChoice();

        var snapshot = GameSnapshot.Capture(state, player);
        var deck = snapshot["deck"]?.AsArray();
        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 1;

        if (deck != null && floor <= 3)
            return IndexChoice(0);

        return MpCheatSyncBot.DefaultIndexChoice();
    }

    static NetPlayerChoiceResult IndexChoice(int index) => new() {
        type = PlayerChoiceType.Index,
        indexes = [index],
    };
}
