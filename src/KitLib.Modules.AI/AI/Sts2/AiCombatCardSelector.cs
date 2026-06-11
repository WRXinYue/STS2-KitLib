using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.Actions;
using KitLib.AI.Combat;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Knowledge;
using KitLib.AI.Planning;
using KitLib.AI.Sts2.Snapshots;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;

namespace KitLib.AI.Sts2;

/// <summary>
/// Resolves in-combat hand prompts (exhaust, discard, upgrade) using deck plan + combat context.
/// Registered via <see cref="MegaCrit.Sts2.Core.Commands.CardSelectCmd.UseSelector"/> — same hook as official AutoSlay,
/// but picks by scored heuristics instead of random shuffle.
/// </summary>
internal sealed class AiCombatCardSelector : ICardSelector {
    readonly Sts2StateProvider _stateProvider;

    public AiCombatCardSelector(Sts2StateProvider stateProvider) {
        _stateProvider = stateProvider;
    }

    public Task<IEnumerable<CardModel>> GetSelectedCards(
        IEnumerable<CardModel> options,
        int minSelect,
        int maxSelect) {
        var list = options.ToList();
        if (list.Count == 0)
            return Task.FromResult((IEnumerable<CardModel>)Array.Empty<CardModel>());

        var count = Math.Clamp(maxSelect, minSelect, list.Count);
        if (count <= 0)
            count = Math.Min(minSelect, list.Count);

        var context = BuildContext();
        var isUpgrade = list.All(c => c.IsUpgradable);

        bool topDeckPick = !isUpgrade && CombatDiscardPickScorer.IsTopDeckPickFromDiscard(list, count);
        var ranked = isUpgrade
            ? list.Where(c => c.CurrentUpgradeLevel < c.MaxUpgradeLevel
                && !CombatCardSelectScoring.IsStatusOrCurse(c, c.Id.Entry ?? ""))
                .OrderByDescending(c => CombatCardSelectScoring.UpgradeScore(c, context))
                .ThenByDescending(c => (int)c.Rarity)
            : topDeckPick
                ? list.OrderByDescending(c => CombatCardSelectScoring.KeepScore(c, context))
                    .ThenBy(c => c.EnergyCost.Canonical)
                : list.OrderBy(c => CombatCardSelectScoring.KeepScore(c, context))
                    .ThenBy(c => c.EnergyCost.Canonical);

        var picked = ranked.Take(count).ToList();
        LogPick(isUpgrade ? "upgrade" : "exhaust/discard", picked, context);
        return Task.FromResult(picked.AsEnumerable());
    }

#if STS2_BETA106PLUS
    public CardRewardSelection GetSelectedCardReward(
        IReadOnlyList<CardCreationResult> options,
        IReadOnlyList<CardRewardAlternative> alternatives) {
        if (options.Count == 0)
            return default;
        return new CardRewardSelection { card = options[0].Card };
    }
#else
    public CardModel? GetSelectedCardReward(
        IReadOnlyList<CardCreationResult> options,
        IReadOnlyList<CardRewardAlternative> alternatives) {
        if (options.Count == 0) return null;
        return options[0].Card;
    }
#endif

    HandSelectContext BuildContext() {
        if (!_stateProvider.TryGetRunAndPlayer(out var state, out var player))
            return HandSelectContext.Empty;

        var snapshot = GameSnapshot.Capture(state, player);
        var plan = DeckPlanInferer.Infer(snapshot);
        var deck = snapshot["deck"]?.AsArray();
        var composition = deck != null
            ? DeckCardScoring.AnalyzeComposition(deck)
            : new DeckComposition(0, 0, 0, 0);
        var needsBlock = IntentCalculator.NeedsBlock(snapshot);

        return new HandSelectContext(snapshot, plan, composition, needsBlock);
    }

    void LogPick(string kind, IReadOnlyList<CardModel> picked, HandSelectContext context) {
        var parts = picked.Select(c => {
            if (kind == "upgrade") {
                var upgrade = CombatCardSelectScoring.UpgradeScore(c, context);
                return $"{c.Title}(upgrade={upgrade})";
            }
            var keep = CombatCardSelectScoring.KeepScore(c, context);
            return $"{c.Title}(keep={keep})";
        });
        var msg = $"Hand select [{kind}]: {string.Join(", ", parts)}";
        MainFile.Logger.Info($"[AiHost] {msg}");
        AiDecisionLog.Record("AutoPlay", msg);
    }
}

internal readonly record struct HandSelectContext(
    JsonObject? Snapshot,
    DeckPlan Plan,
    DeckComposition Composition,
    bool NeedsBlock) {
    public static HandSelectContext Empty => new(null, new DeckPlan(), new DeckComposition(0, 0, 0, 0), false);
    public bool HasSnapshot => Snapshot != null;
}

internal static class CombatCardSelectScoring {
    /// <summary>Higher = more valuable to keep (exhaust/discard lowest scores).</summary>
    public static int KeepScore(CardModel card, HandSelectContext context) {
        var id = card.Id.Entry ?? "";
        var upper = id.ToUpperInvariant();

        if (IsStatusOrCurse(card, upper))
            return -200;

        if (CardMechanicIndex.TryGet(id, out var profile)
            && MechanicCombatBonus.IsSetupSkill(profile))
            return 120;

        if (context.HasSnapshot) {
            var cardJson = SnapshotCardJson.FromCard(card);
            var keep = DeckCardScoring.ScoreInDeck(cardJson, context.Plan, context.Composition);

            if (context.NeedsBlock && (upper.Contains("DEFEND", StringComparison.Ordinal) || CardEditActions.GetBlock(card) > 0))
                keep += 35;

            return keep;
        }

        return FallbackKeepScore(card, upper);
    }

    static int FallbackKeepScore(CardModel card, string upper) {
        if (upper.Contains("STRIKE", StringComparison.Ordinal))
            return card.CurrentUpgradeLevel > 0 ? 25 : 5;
        if (upper.Contains("DEFEND", StringComparison.Ordinal))
            return card.CurrentUpgradeLevel > 0 ? 20 : 8;

        var score = card.Rarity switch {
            CardRarity.Rare => 90,
            CardRarity.Uncommon => 60,
            CardRarity.Common => 35,
            CardRarity.Basic => 15,
            _ => 40,
        };
        if (card.CurrentUpgradeLevel > 0) score += 10;
        score += Math.Min(6, CardEditActions.GetDamage(card) ?? 0);
        score += Math.Min(4, CardEditActions.GetBlock(card) ?? 0);
        return score;
    }

    /// <summary>Higher = upgrade first (core build cards over strikes).</summary>
    public static int UpgradeScore(CardModel card, HandSelectContext context) {
        if (card.CurrentUpgradeLevel >= card.MaxUpgradeLevel)
            return int.MinValue;

        var id = card.Id.Entry ?? "";
        if (IsStatusOrCurse(card, id.ToUpperInvariant()))
            return int.MinValue;

        if (context.HasSnapshot) {
            var cardJson = SnapshotCardJson.FromCard(card);
            return DeckCardScoring.ScoreUpgradeCandidate(
                cardJson, context.Plan, context.Composition, context.Snapshot);
        }

        return FallbackUpgradeScore(card, id);
    }

    static int FallbackUpgradeScore(CardModel card, string id) {
        var upper = id.ToUpperInvariant();
        int score = card.Rarity switch {
            CardRarity.Rare => 70,
            CardRarity.Uncommon => 45,
            CardRarity.Common => 25,
            CardRarity.Basic => 8,
            _ => 30,
        };
        if (upper.Contains("STRIKE", StringComparison.Ordinal)) score -= 50;
        if (upper.Contains("DEFEND", StringComparison.Ordinal)) score -= 35;
        return score;
    }

    internal static bool IsStatusOrCurse(CardModel card, string idUpper) {
        if (card.Rarity == CardRarity.Curse || card.Rarity == CardRarity.Status)
            return true;

        return idUpper.Contains("BURN", StringComparison.Ordinal)
            || idUpper.Contains("SLIMED", StringComparison.Ordinal)
            || idUpper.Contains("SLIME", StringComparison.Ordinal)
            || idUpper.Contains("WOUND", StringComparison.Ordinal)
            || idUpper.Contains("DORMANT", StringComparison.Ordinal)
            || idUpper.Contains("VOID", StringComparison.Ordinal);
    }
}
