using System.Collections.Generic;

namespace KitLib.Singleplayer.Companion;

/// <summary>Singleplayer companion session lifecycle.</summary>
internal static class SpvCompanionSession {
    static readonly HashSet<ulong> _awaitingCombatBootstrap = [];

    public static void OnCompanionSpawned() {
        KitLog.Info("SpCompanion", "Singleplayer companion registered.");
    }

    internal static void MarkCombatBootstrapPending(ulong netId) {
        if (netId != 0)
            _awaitingCombatBootstrap.Add(netId);
    }

    internal static void MarkCombatBootstrapComplete(ulong netId) {
        if (netId != 0)
            _awaitingCombatBootstrap.Remove(netId);
    }

    internal static bool IsAwaitingCombatBootstrap(ulong netId) =>
        netId != 0 && _awaitingCombatBootstrap.Contains(netId);

    public static void OnRunEnded() {
        _awaitingCombatBootstrap.Clear();
        SpvCompanionRegistry.Clear();
    }
}
