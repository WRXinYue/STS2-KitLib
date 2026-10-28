using Godot;
using HarmonyLib;
using KitLib.Replay.Commands;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace KitLib.Replay.Patches;

/// <summary>
/// Manual patch on <c>NRewardButton.GetReward</c> so reward clicks record
/// <see cref="ClaimRewardCommand"/> (Godot generates a concrete subclass).
/// </summary>
internal static class CardRewardButtonPatcher {
    static bool _applied;

    internal static void Apply() {
        if (_applied)
            return;
        _applied = true;

        try {
            var harmony = new Harmony("KitLib.Replay.CardRewardButton");
            var nRewardButtonType = typeof(NRewardsScreen).Assembly
                .GetType("MegaCrit.Sts2.Core.Nodes.Rewards.NRewardButton");
            if (nRewardButtonType == null)
                return;

            var getReward = AccessTools.Method(nRewardButtonType, "GetReward");
            if (getReward == null)
                return;

            harmony.Patch(getReward, prefix: new HarmonyMethod(typeof(CardRewardButtonPatcher), nameof(GetRewardPrefix)));
        }
        catch (Exception ex) {
            PlayerActionBuffer.LogToDevConsole($"[CardRewardButtonPatcher] {ex.Message}");
        }
    }

    public static void GetRewardPrefix(object __instance) {
        if (ReplayEngine.IsActive)
            return;

        Node node = (Node)__instance;
        Node? current = node.GetParent();
        NRewardsScreen? screen = null;
        while (current != null) {
            if (current is NRewardsScreen s) {
                screen = s;
                break;
            }
            current = current.GetParent();
        }

        if (screen == null)
            return;

        int index = 0;
        foreach (var (button, reward) in ClaimRewardCommand.EnumerateRewardButtons(screen)) {
            if (ReferenceEquals(button, node)) {
                var cmd = new ClaimRewardCommand(index) {
                    Comment = ClaimRewardCommand.DescribeReward(reward),
                };
                PlayerActionBuffer.Record(cmd.ToLogString());
                return;
            }
            index++;
        }
    }
}
