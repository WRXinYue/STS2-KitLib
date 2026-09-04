using System.Collections;
using System.Reflection;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.Play;

/// <summary>Which run peers the host must enqueue combat for, versus local play.</summary>
internal static class HostDrivenPeers {
    static HashSet<ulong> _simulatedPeerNetIds = [];

    public static bool IsHostMultiplayer =>
        MpCheatSession.InMultiplayerRun && MpCheatSession.IsHost;

    public static bool IsRegistryActive =>
        IsHostMultiplayer
        && (AiSessionSettings.SyncBotEnabled || AiSessionSettings.MpAiTeammateEnabled);

    public static bool IsLiveEnetPeer(ulong netId) {
        if (netId == 0) return false;
        var net = RunManager.Instance?.NetService;
        if (net is null) return false;
        object? peers = net.GetType().GetProperty("ConnectedPeers")?.GetValue(net);
        if (peers is not IEnumerable enumerable)
            return false;

        foreach (var item in enumerable) {
            if (item is null) continue;
            var type = item.GetType();
            var id = type.GetField("peerId")?.GetValue(item) ?? type.GetProperty("peerId")?.GetValue(item);
            if (id is ulong u && u == netId)
                return true;
        }

        return false;
    }

    public static IEnumerable<Player> GetPeersNeedingSimulation() {
        var run = RunManager.Instance;
        var state = run?.DebugOnlyGetState();
        var hostNetId = run?.NetService?.NetId ?? 0;
        if (state == null || hostNetId == 0) return [];

        return state.Players.Where(p => p.NetId != hostNetId && !IsLiveEnetPeer(p.NetId));
    }

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

    public static HashSet<ulong> GetAckPeerNetIds() {
        if (!AiSessionSettings.SyncBotEnabled
            || !IsHostMultiplayer
            || !MpCheatSession.CanUseMultiplayerCheats)
            return [];
        return GetSimulatedPeerNetIds();
    }

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
        AiSessionSettings.MpAiTeammateDriveLiveEnet;

    public static bool IsHostDrivenPeer(ulong netId) {
        if (netId == 0 || !MpCheatSession.InMultiplayerRun) return false;
        var hostNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        if (hostNetId == 0 || netId == hostNetId) return false;

        if (IsSimulatedPeer(netId)) return true;
        return DriveLiveEnetEnabled
            && MpCheatSession.IsHost
            && AiSessionSettings.MpAiTeammateEnabled
            && IsLiveEnetPeer(netId);
    }

    public static IEnumerable<Player> GetMpAiTeammateTargets() {
        var state = RunManager.Instance?.DebugOnlyGetState();
        var hostNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        if (state == null || hostNetId == 0) return [];
        if (!AiSessionSettings.MpAiTeammateEnabled || !IsHostMultiplayer) return [];

        return state.Players.Where(p => p.NetId != hostNetId && IsHostDrivenPeer(p.NetId));
    }

    public static bool IsMpAiTeammateTarget(ulong netId) =>
        GetMpAiTeammateTargets().Any(p => p.NetId == netId);

    public static IEnumerable<Player> GetHostDrivenCombatPeers() {
        var state = RunManager.Instance?.DebugOnlyGetState();
        var hostNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        if (state == null || hostNetId == 0 || !IsHostMultiplayer) return [];

        return state.Players.Where(p => {
            if (p.NetId == hostNetId) return false;
            if (IsLiveEnetPeer(p.NetId)) return DriveLiveEnetEnabled;
            return true;
        });
    }

    public static bool ShouldHostRouteCombatEnqueue(Player player) {
        if (!MpCheatSession.InMultiplayerRun) return false;
        if (RunManager.Instance?.NetService?.Type != NetGameType.Host) return false;
        var hostNetId = RunManager.Instance.NetService.NetId;
        if (player.NetId == hostNetId) return false;
        return DriveLiveEnetEnabled && IsLiveEnetPeer(player.NetId);
    }

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
        if (AiSessionSettings.MpAiTeammateEnabled && IsHostMultiplayer)
            return GetMpAiTeammateTargets().ToList();
        return GetPeersNeedingSimulation().ToList();
    }

    public static IEnumerable<Player> GetMapMirrorTargets() {
        if (!IsRegistryActive) return [];
        if (DriveLiveEnetEnabled && AiSessionSettings.MpAiTeammateEnabled && IsHostMultiplayer)
            return GetMpAiTeammateTargets().ToList();
        return GetPeersNeedingSimulation().ToList();
    }

    public static void OnRunEnded() => _simulatedPeerNetIds.Clear();
}
