using System;
using System.Text.Json.Nodes;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Planning;

public readonly record struct DeckStats(
    int DeckSize,
    int AttackCount,
    int SkillCount,
    int PowerCount,
    int StrikeCount,
    int DefendCount,
    int CurseCount) {
    public static DeckStats From(JsonArray? deck) {
        if (deck == null || deck.Count == 0)
            return new DeckStats(0, 0, 0, 0, 0, 0, 0);

        int attacks = 0, skills = 0, powers = 0, strikes = 0, defends = 0, curses = 0;
        foreach (var node in deck) {
            if (node is not JsonObject card) continue;
            var id = (card["id"]?.GetValue<string>() ?? "").ToUpperInvariant();
            var type = card["cardType"]?.GetValue<string>() ?? "";
            var rarity = (card["rarity"]?.GetValue<string>() ?? "").ToUpperInvariant();

            if (type.Contains("Attack", StringComparison.OrdinalIgnoreCase) || card["damage"] != null)
                attacks++;
            if (type.Contains("Skill", StringComparison.OrdinalIgnoreCase))
                skills++;
            if (type.Contains("Power", StringComparison.OrdinalIgnoreCase))
                powers++;
            if (id.Contains("STRIKE", StringComparison.Ordinal))
                strikes++;
            if (id.Contains("DEFEND", StringComparison.Ordinal))
                defends++;
            if (rarity.Contains("CURSE", StringComparison.Ordinal))
                curses++;
        }

        return new DeckStats(deck.Count, attacks, skills, powers, strikes, defends, curses);
    }
}

/// <summary>Deck-context synergy from indexed card/relic mechanics (not per-id tables).</summary>
public static class DeckSynergyEvaluator {
    public static int ScoreCard(JsonObject card, DeckPlan plan, JsonObject? snapshot) {
        var profile = ResolveCardProfile(card);
        var stats = DeckStats.From(snapshot?["deck"]?.AsArray());
        var cost = card["cost"]?.GetValue<int>() ?? profile.CanonicalCost;
        int score = 0;

        if (profile.Flags.HasFlag(CardMechanicFlags.TransformsHandAttacks)) {
            var attackPool = Math.Max(stats.AttackCount, stats.StrikeCount);
            score += Math.Min(attackPool, 7) * 4;
            if (cost <= 0)
                score += 10;
        }
        else if (profile.Flags.HasFlag(CardMechanicFlags.TransformsCards)) {
            score += Math.Min(Math.Max(stats.AttackCount, stats.StrikeCount), 5) * 2;
        }

        if (profile.Flags.HasFlag(CardMechanicFlags.HasDraw))
            score += (int)Math.Round(plan.GetWeight(AiTag.Draw) * 6f);
        if (profile.Flags.HasFlag(CardMechanicFlags.HasForge))
            score += (int)Math.Round(plan.GetWeight(AiTag.Scaling) * 7f);
        if (profile.Flags.HasFlag(CardMechanicFlags.HasStarCost))
            score += (int)Math.Round(plan.GetWeight(AiTag.Energy) * 5f);
        if (profile.Flags.HasFlag(CardMechanicFlags.HasSummon))
            score += (int)Math.Round(plan.GetWeight(AiTag.Scaling) * 4f);
        if (profile.Flags.HasFlag(CardMechanicFlags.Aoe))
            score += (int)Math.Round(plan.GetWeight(AiTag.Aoe) * 5f);
        if (profile.Flags.HasFlag(CardMechanicFlags.Exhaust))
            score += (int)Math.Round(plan.GetWeight(AiTag.Exhaust) * 3f);

        foreach (var tag in profile.DerivedTags)
            score += (int)Math.Round(plan.GetWeight(tag) * 1.5f);

        if (profile.Installs(PlayerPowerEffectKind.InfernoRetaliate)) {
            int selfDamageSources = CountSelfDamageSources(snapshot?["deck"]?.AsArray());
            score += Math.Min(selfDamageSources, 5) * 7;
            if (selfDamageSources == 0)
                score -= 14;
        }

        return score;
    }

    static int CountSelfDamageSources(JsonArray? deck) {
        if (deck == null || deck.Count == 0)
            return 0;

        int count = 0;
        foreach (var node in deck) {
            if (node is not JsonObject card) continue;
            var profile = ResolveCardProfile(card);
            if (profile.HpLoss > 0)
                count++;
        }

        return count;
    }

    public static int ScoreDeckDilutionOffer(
        JsonObject card,
        DeckPlan plan,
        DeckMetrics metrics,
        JsonArray? deck) {
        var profile = ResolveCardProfile(card);
        if (!profile.Flags.HasFlag(CardMechanicFlags.AddsCardsToDeck))
            return 0;

        int penalty = 6 + metrics.ThinGap * 4;
        if (DeckEvaluator.HasTransformCore(deck))
            penalty += 10;
        penalty += (int)Math.Round(plan.ThinPreference * 8f);
        if (metrics.BlockDeficit == 0 && metrics.DrawDeficit == 0)
            penalty += 6;
        return -penalty;
    }

    public static int ScoreRelic(string? relicId, DeckPlan plan, JsonObject? snapshot) {
        if (string.IsNullOrWhiteSpace(relicId))
            return 0;
        if (!RelicMechanicIndex.TryGet(relicId, out var profile))
            return 0;

        var stats = DeckStats.From(snapshot?["deck"]?.AsArray());
        int score = 0;

        if (profile.Flags.HasFlag(RelicMechanicFlags.OffersRarePick))
            score += 14 + Math.Min(stats.DeckSize, 12);
        if (profile.Flags.HasFlag(RelicMechanicFlags.OffersCardPick))
            score += 10;
        if (profile.Flags.HasFlag(RelicMechanicFlags.RemovesCard))
            score += (int)Math.Round(plan.ThinPreference * 12f);
        if (profile.Flags.HasFlag(RelicMechanicFlags.CombatScaling))
            score += (int)Math.Round(plan.GetWeight(AiTag.Scaling) * 4f);

        if (profile.Flags.HasFlag(RelicMechanicFlags.AddsCurseOrInjury))
            score -= (int)Math.Round(10f + plan.ThinPreference * 12f + stats.CurseCount * 2f);

        if (profile.Flags.HasFlag(RelicMechanicFlags.AddsMaxHp))
            score -= 4;

        foreach (var tag in profile.DerivedTags)
            score += (int)Math.Round(plan.GetWeight(tag) * 2f);

        return score;
    }

    public static int RelicTagPlanScore(string? relicId, DeckPlan plan) {
        if (string.IsNullOrWhiteSpace(relicId))
            return 0;
        int score = 0;
        foreach (var tag in RelicCatalog.ResolveTags(relicId))
            score += (int)Math.Round(plan.GetWeight(tag) * 3f);
        return score;
    }

    static CardMechanicProfile ResolveCardProfile(JsonObject card) {
        var id = card["id"]?.GetValue<string>();
        if (CardMechanicIndex.TryGet(id, out var profile))
            return profile;
        return CardMechanicIndex.InferFromSnapshot(card);
    }
}
