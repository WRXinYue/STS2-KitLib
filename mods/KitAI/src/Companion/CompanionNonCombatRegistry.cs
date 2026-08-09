using System.Collections.Generic;

namespace KitLib.Companion;

/// <summary>Tracks companions that should run non-combat AI (events, rewards, rest, shop).</summary>
public static class CompanionNonCombatRegistry {
    static readonly HashSet<ulong> EnabledNetIds = [];

    public static void Enable(ulong netId) {
        if (netId == 0) return;
        EnabledNetIds.Add(netId);
    }

    public static void Disable(ulong netId) => EnabledNetIds.Remove(netId);

    public static bool IsEnabled(ulong netId) => EnabledNetIds.Contains(netId);

    internal static void ClearOnRunEnd() => EnabledNetIds.Clear();
}
