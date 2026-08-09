using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.CombatStats;

/// <summary>Infers relic/power/card sources from call-site declaring types when history omits them.</summary>
internal static class CombatStatsCallSiteResolver {
    private static readonly Dictionary<Type, CombatStatSource?> Cache = new();

    public static CombatStatSource? TryResolveFromStack(int skipFrames = 1, Creature? contextCreature = null) {
        var trace = new StackTrace(skipFrames, fNeedFileInfo: false);
        int limit = Math.Min(trace.FrameCount, 40);
        for (int i = 0; i < limit; i++) {
            var frame = trace.GetFrame(i);
            var declaring = frame?.GetMethod()?.DeclaringType;
            if (declaring == null || ShouldSkipFrame(declaring))
                continue;

            var resolved = ResolveDeclaringType(declaring, contextCreature);
            if (resolved is { IsKnown: true })
                return resolved;
        }

        return null;
    }

    private static bool ShouldSkipFrame(Type declaring) {
        if (declaring == typeof(CombatStatsCallSiteResolver))
            return true;

        string ns = declaring.Namespace ?? "";
        return ns.StartsWith("System", StringComparison.Ordinal)
            || ns.StartsWith("HarmonyLib", StringComparison.Ordinal)
            || ns.StartsWith("KitLib", StringComparison.Ordinal)
            || ns == "MegaCrit.Sts2.Core.Commands";
    }

    private static CombatStatSource? ResolveDeclaringType(Type type, Creature? contextCreature) {
        if (Cache.TryGetValue(type, out var cached))
            return cached;

        CombatStatSource? source = null;
        if (typeof(RelicModel).IsAssignableFrom(type))
            source = TryResolveModel(ModelDb.AllRelics, type, CombatStatSource.FromRelic)
                ?? TryResolveOwnedRelic(type, contextCreature);
        else if (typeof(PowerModel).IsAssignableFrom(type) && !type.IsAbstract)
            source = TryResolveModel(ModelDb.AllPowers, type, CombatStatSource.FromPower);
        else if (typeof(CardModel).IsAssignableFrom(type) && !type.IsAbstract)
            source = TryResolveModel(ModelDb.AllCards, type, CombatStatSource.FromCard);
        else if (typeof(PotionModel).IsAssignableFrom(type) && !type.IsAbstract)
            source = TryResolveModel(ModelDb.AllPotions, type, CombatStatSource.FromPotion);
        else if (typeof(MonsterModel).IsAssignableFrom(type) && !type.IsAbstract)
            source = TryResolveModel(ModelDb.Monsters, type, CombatStatSource.FromMonster);

        Cache[type] = source;
        return source;
    }

    private static CombatStatSource? TryResolveOwnedRelic(Type declaring, Creature? contextCreature) {
        Player? player = contextCreature?.Player;
        if (player == null)
            return null;

        RelicModel? match = null;
        foreach (RelicModel relic in player.Relics) {
            if (!declaring.IsAssignableFrom(relic.GetType()))
                continue;
            if (match != null)
                return null;
            match = relic;
        }

        return match == null ? null : CombatStatSource.FromRelic(match);
    }

    private static CombatStatSource? TryResolveModel<T>(
        IEnumerable<T> models,
        Type type,
        Func<T, CombatStatSource> map)
        where T : AbstractModel {
        foreach (var model in models) {
            if (model.GetType() == type)
                return map(model);
        }

        foreach (var model in models) {
            if (model.GetType().Name == type.Name)
                return map(model);
        }

        return null;
    }
}
