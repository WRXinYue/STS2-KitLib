using System;
using System.Collections.Generic;
using KitLib.AI.Core;

namespace KitLib.Companion;

public static class CompanionRegistry {
    static readonly Dictionary<ulong, IDecisionMaker> Strategies = [];

    public static bool TryGet(ulong netId, out IDecisionMaker strategy) =>
        Strategies.TryGetValue(netId, out strategy!);

    public static void Register(ulong netId, IDecisionMaker strategy) {
        ArgumentNullException.ThrowIfNull(strategy);
        Strategies[netId] = strategy;
        MainFile.Logger.Info($"[Companion] Strategy registered netId={netId}.");
    }

    public static void Unregister(ulong netId) {
        if (Strategies.Remove(netId))
            MainFile.Logger.Info($"[Companion] Strategy unregistered netId={netId}.");
    }

    public static void ClearOnRunEnd() => Strategies.Clear();
}
