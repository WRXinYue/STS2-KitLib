using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.Cheat;

internal static class MpCheatPlayerLabels {
    internal static string FormatPickerLabel(Player player) {
        var character = player.Character?.Id.Entry ?? "?";
        var localNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        var suffix = player.NetId == localNetId
            ? I18N.T("mpcheat.cardAdd.targetPlayerLocal", " (you)")
            : I18N.T("mpcheat.cardAdd.targetPlayerRemote", " (remote)");
        return $"{character}{suffix}";
    }

    internal static string FormatLogLabel(Player player) {
        var character = player.Character?.Id.Entry ?? "?";
        return $"{character}#{player.NetId}";
    }
}
