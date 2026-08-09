using System;
using KitLib.AI.AutoPlay.Strategies;
using KitLib.AI.Core;
using KitLib.AI.Planning;
using KitLib.Companion;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.AI.Core;

/// <summary>Resolves <see cref="IDecisionMaker"/> for a companion or player (netId → characterId → fallback).</summary>
public static class StrategyResolver {
    static readonly IDecisionMaker SimpleFallback = new SimpleStrategy();
    static readonly IDecisionMaker StrongFallback = new StrongStrategy();

    public static IDecisionMaker Resolve(ulong netId, Player? player) {
        if (netId != 0 && CompanionRegistry.TryGet(netId, out var perNet))
            return perNet;

        var characterId = player?.Character?.Id.Entry;
        if (CharacterAiRegistry.TryGet(characterId, out var byCharacter))
            return byCharacter;

        return DefaultFallback;
    }

    public static IDecisionMaker Resolve(Player player) => Resolve(player.NetId, player);

    static IDecisionMaker DefaultFallback =>
        string.Equals(SettingsStore.Current.AutoPlayStrategy, "Simple", StringComparison.OrdinalIgnoreCase)
            ? SimpleFallback
            : StrongFallback;
}
