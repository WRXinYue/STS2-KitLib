using System.Collections.Generic;
using System.Linq;
using KitLib.Multiplayer.Cheat;
using KitLib.Multiplayer.SyncBot;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>Unified roster for SyncBot ACKs vs in-process teammate simulation.</summary>
internal static class SimulatedPeerRegistry {
    static HashSet<ulong> _simulatedPeerNetIds = [];

    public static bool IsHostMultiplayer =>
        MpCheatSession.InMultiplayerRun && MpCheatSession.IsHost;

    public static bool IsRegistryActive =>
        IsHostMultiplayer
        && (SettingsStore.Current.SyncBotEnabled || SettingsStore.Current.MpAiTeammateEnabled);

    /// <summary>True when <paramref name="netId"/> is a real ENet lobby peer (not phantom/debug).</summary>
    public static bool IsLiveEnetPeer(ulong netId) {
        if (netId == 0 || netId == MpCheatSyncBot.PhantomPlayerNetId) return false;
        if (RunManager.Instance?.NetService is not NetHostGameService host) return false;
        return host.ConnectedPeers.Any(p => p.peerId == netId);
    }

    /// <summary>Run players that need in-process votes/choices (phantom 1001, etc.).</summary>
    public static IEnumerable<Player> GetPeersNeedingSimulation() {
        var run = RunManager.Instance;
        var state = run?.DebugOnlyGetState();
        var hostNetId = run?.NetService?.NetId ?? 0;
        if (state == null || hostNetId == 0) return [];

        return state.Players.Where(p => p.NetId != hostNetId && !IsLiveEnetPeer(p.NetId));
    }

    /// <summary>Remote peers in the run (non-host).</summary>
    public static HashSet<ulong> GetRemoteRunNetIds() {
        var run = RunManager.Instance;
        var hostNetId = run?.NetService?.NetId ?? 0;
        var state = run?.DebugOnlyGetState();
        if (state == null || hostNetId == 0) return [];

        return state.Players
            .Select(p => p.NetId)
            .Where(id => id != hostNetId)
            .ToHashSet();
    }

    /// <summary>All remote run peers — used for MpCheat prepare ACK injection when SyncBot is on.</summary>
    public static HashSet<ulong> GetAckPeerNetIds() {
        if (!SettingsStore.Current.SyncBotEnabled
            || !IsHostMultiplayer
            || !MpCheatSession.CanUseMultiplayerCheats)
            return [];
        return GetSimulatedPeerNetIds();
    }

    /// <summary>Net ids for auto-vote / combat sync injection.</summary>
    public static HashSet<ulong> GetSimulatedPeerNetIds() {
        if (!IsRegistryActive) return [];
        return GetPeersNeedingSimulation().Select(p => p.NetId).ToHashSet();
    }

    public static void Refresh() {
        _simulatedPeerNetIds = GetSimulatedPeerNetIds();
    }

    public static bool IsSimulatedPeer(ulong netId) =>
        IsRegistryActive && _simulatedPeerNetIds.Contains(netId);

    public static bool DriveLiveEnetEnabled =>
        SettingsStore.Current.MpAiTeammateDriveLiveEnet;

    /// <summary>Phantom/offline simulated peers, or live ENet when LAN host-drive is on.</summary>
    public static bool IsHostDrivenPeer(ulong netId) {
        if (netId == 0 || !MpCheatSession.InMultiplayerRun) return false;
        var hostNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        if (hostNetId == 0 || netId == hostNetId) return false;

        if (IsSimulatedPeer(netId)) return true;
        return DriveLiveEnetEnabled
            && MpCheatSession.IsHost
            && SettingsStore.Current.MpAiTeammateEnabled
            && IsLiveEnetPeer(netId);
    }

    /// <summary>Host AI teammate targets: simulated peers and optional live ENet clients.</summary>
    public static IEnumerable<Player> GetMpAiTeammateTargets() {
        var state = RunManager.Instance?.DebugOnlyGetState();
        var hostNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        if (state == null || hostNetId == 0) return [];
        if (!SettingsStore.Current.MpAiTeammateEnabled || !IsHostMultiplayer) return [];

        return state.Players.Where(p => p.NetId != hostNetId && IsHostDrivenPeer(p.NetId));
    }

    public static bool IsMpAiTeammateTarget(ulong netId) =>
        GetMpAiTeammateTargets().Any(p => p.NetId == netId);

    /// <summary>Remote peers in LAN host-drive or phantom assist (independent of AI poll toggle).</summary>
    public static IEnumerable<Player> GetHostDrivenCombatPeers() {
        var state = RunManager.Instance?.DebugOnlyGetState();
        var hostNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        if (state == null || hostNetId == 0 || !IsHostMultiplayer) return [];

        return state.Players.Where(p => {
            if (p.NetId == hostNetId) return false;
            if (IsLiveEnetPeer(p.NetId)) return DriveLiveEnetEnabled;
            return !IsLiveEnetPeer(p.NetId);
        });
    }

    /// <summary>Owner-routed combat enqueue for LAN live peers (does not require AI poll on).</summary>
    public static bool ShouldHostRouteCombatEnqueue(Player player) {
        if (!MpCheatSession.InMultiplayerRun) return false;
        if (RunManager.Instance?.NetService?.Type != NetGameType.Host) return false;
        var hostNetId = RunManager.Instance.NetService.NetId;
        if (player.NetId == hostNetId) return false;
        return DriveLiveEnetEnabled && IsLiveEnetPeer(player.NetId);
    }

    /// <summary>Host must enqueue combat actions for host-driven peers (never CardCmd.AutoPlay).</summary>
    public static bool ShouldHostEnqueueCombatAction(Player player) {
        if (!MpCheatSession.InMultiplayerRun) return false;
        if (RunManager.Instance?.NetService?.Type != NetGameType.Host) return false;
        var hostNetId = RunManager.Instance.NetService.NetId;
        if (player.NetId == hostNetId) return false;
        return IsHostDrivenPeer(player.NetId);
    }

    public static bool HasLiveEnetTeammate() {
        var state = RunManager.Instance?.DebugOnlyGetState();
        var hostNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        if (state == null || hostNetId == 0) return false;
        return state.Players.Any(p => p.NetId != hostNetId && IsLiveEnetPeer(p.NetId));
    }

    public static IEnumerable<Player> GetRemoteCombatAssistTargets() {
        Refresh();
        if (SettingsStore.Current.MpAiTeammateEnabled && IsHostMultiplayer)
            return GetMpAiTeammateTargets().ToList();
        return GetPeersNeedingSimulation().ToList();
    }

    /// <summary>Peers that receive mirrored host map / act-ready votes.</summary>
    public static IEnumerable<Player> GetMapMirrorTargets() {
        if (!IsRegistryActive) return [];
        if (DriveLiveEnetEnabled && SettingsStore.Current.MpAiTeammateEnabled && IsHostMultiplayer)
            return GetMpAiTeammateTargets().ToList();
        return GetPeersNeedingSimulation().ToList();
    }

    public static void OnRunEnded() => _simulatedPeerNetIds.Clear();
}
