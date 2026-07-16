using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace KitLib.CombatStats;

internal static class CombatStatsSourceResolver {
    public static CombatStatSource ResolveBlock(CardPlay? cardPlay, ValueProp props, Creature? receiver) {
        if (cardPlay?.Card is CardModel card)
            return CombatStatSource.FromCard(card);

        var fromStack = CombatStatsCallSiteResolver.TryResolveFromStack(skipFrames: 2, contextCreature: receiver);
        if (fromStack is { IsKnown: true })
            return fromStack.Value;

        if (props.HasFlag(ValueProp.Move))
            return CombatStatSource.MonsterMove();

        return CombatStatSource.Unknown;
    }

    public static CombatStatSource ResolvePowerApply(PowerModel power, Creature? applier) {
        Creature? context = applier ?? power.Owner;

        var fromStack = CombatStatsCallSiteResolver.TryResolveFromStack(skipFrames: 2, contextCreature: context);
        if (fromStack is { IsKnown: true })
            return fromStack.Value;

        if (applier != null) {
            var fromApplier = CombatStatSource.FromCreature(applier);
            if (fromApplier.IsKnown && fromApplier.Kind != CombatStatSourceKind.Player)
                return fromApplier;
        }

        return CombatStatSource.Unknown;
    }
}
