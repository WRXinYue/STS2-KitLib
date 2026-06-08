using KitLib.Multiplayer.Cheat;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>
/// LAN dual-instance AI ownership: each window drives only its local player off-combat;
/// host drives remote peers in combat via <see cref="MpAiTeammateHost"/>.
/// </summary>
internal static class LanAiOwnership {
    public static bool IsLiveLanHost =>
        MpCheatSession.IsHost
        && MpCheatSession.InMultiplayerRun
        && SettingsStore.Current.MpAiTeammateDriveLiveEnet
        && SimulatedPeerRegistry.HasLiveEnetTeammate();

    /// <summary>Companion targets phantom peers; live LAN uses per-instance LanLocal instead.</summary>
    public static bool ShouldRunCompanionHost =>
        SettingsStore.Current.MpAiTeammateEnabled
        && MpCheatSession.IsHost
        && MpCheatSession.InMultiplayerRun
        && !IsLiveLanHost;

    public static bool TryGetLocalPlayer(out Player player) {
        player = null!;
        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null) return false;

        player = LocalContext.GetMe(state.Players);
        return player != null;
    }

    public static bool IsLocalPlayer(Player player) =>
        player != null && LocalContext.IsMe(player);

    public static bool IsHostHandPlayLocal(Player player) =>
        IsLiveLanHost && IsLocalPlayer(player);
}
