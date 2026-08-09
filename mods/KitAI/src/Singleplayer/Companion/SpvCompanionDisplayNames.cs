using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Singleplayer.Companion;

internal static class SpvCompanionDisplayNames {
    internal static bool ShouldOverride(Player? player) {
        if (player == null || !SpvCompanionRegistry.HasAny)
            return false;

        var run = RunManager.Instance;
        if (run?.IsSingleplayerOrFakeMultiplayer != true)
            return false;

        var state = run.DebugOnlyGetState();
        return state != null && state.Players.Count > 1;
    }

    internal static string Resolve(Player player) {
        if (SpvCompanionRegistry.IsCompanion(player))
            return player.Character?.Title.GetFormattedText() ?? $"Companion {player.NetId}";

        if (IsLocalHuman(player))
            return ResolveLocalPlatformName();

        return player.Character?.Title.GetFormattedText() ?? player.NetId.ToString();
    }

    static bool IsLocalHuman(Player player) {
        if (LocalContext.IsMe(player))
            return true;

        var state = RunManager.Instance?.DebugOnlyGetState();
        return state != null && player.NetId == SpvCompanionRegistry.GetLocalNetId(state);
    }

    static string ResolveLocalPlatformName() {
        var platform = RunManager.Instance!.NetService.Platform;
        var platformLocalId = PlatformUtil.GetLocalPlayerId(platform);
        return PlatformUtil.GetPlayerNameRaw(platform, platformLocalId);
    }
}
