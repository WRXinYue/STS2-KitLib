using System.Collections.Generic;
using System.Linq;
using KitLib.AI;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Singleplayer.Companion;

/// <summary>Tracks AI companions spawned during <see cref="NetGameType.Singleplayer"/> runs.</summary>
internal static class SpvCompanionRegistry {
    static readonly HashSet<ulong> _companionNetIds = [];

    public static bool HasAny => _companionNetIds.Count > 0;

    public static void Register(ulong netId) {
        if (netId == 0) return;

        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state != null && netId == GetLocalNetId(state)) {
            KitLog.Warn("SpCompanion", $"Refusing to register local netId={netId} as companion.");
            return;
        }

        _companionNetIds.Add(netId);
    }

    public static void Unregister(ulong netId) => _companionNetIds.Remove(netId);

    public static bool IsCompanion(ulong netId) => _companionNetIds.Contains(netId);

    public static bool IsCompanion(Player? player) =>
        player != null && IsCompanion(player.NetId);

    public static IEnumerable<Player> GetCombatTargets() {
        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null || !IsSingleplayerRun()) return [];

        return state.Players.Where(p => IsCompanion(p.NetId) && !LocalContext.IsMe(p));
    }

    public static bool IsAiDriven(ulong netId) => IsCompanion(netId);

    public static void Clear() => _companionNetIds.Clear();

    internal static bool IsSingleplayerRun() =>
        RunManager.Instance?.NetService?.Type == NetGameType.Singleplayer;

    internal static ulong GetLocalNetId(RunState state) {
        var fromNet = RunManager.Instance?.NetService?.NetId ?? 0;
        if (fromNet != 0)
            return fromNet;

        var local = LocalContext.GetMe(state.Players);
        return local?.NetId ?? state.Players.FirstOrDefault()?.NetId ?? 1;
    }
}
