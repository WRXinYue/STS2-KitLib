using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Planning;

public sealed record MacroResourceSnapshot(
    int Hp,
    int MaxHp,
    int Gold,
    int DeckSize,
    int ActIndex,
    int TotalFloor,
    int RouteFightScore,
    string PhaseLabel);

public sealed record ScoringPhaseWeights(
    float CurrentSimWeight,
    float OptionWeight,
    float DilutionWeight,
    string PhaseLabel,
    string Rationale);

public sealed record DeckArchetypeInsight(
    string Id,
    string Role,
    int DeckPieces,
    int RelicPieces,
    int ScoreContribution);

public sealed record DeckComboScore(
    int RouteFightScore,
    int DeckQualityScore,
    int SurvivalGap,
    int ThinGap,
    int StarterBloat,
    IReadOnlyList<DeckArchetypeInsight> Archetypes);

public sealed record CardRoleInsight(
    string PrimaryRole,
    bool FightFuture,
    string Reason,
    int InRunScore,
    int OutRunScore,
    IReadOnlyList<string> ArchetypeIds);

/// <summary>Macro scoring labels for Dev Viewer: in-run vs out-of-run, transition/terminal/system.</summary>
public static class MacroScoringInsights {
    public static MacroResourceSnapshot BuildResources(JsonObject snapshot, DeckPlan plan) {
        int route = DeckSimScorer.HasRoutePreview(snapshot)
            ? DeckSimScorer.ScoreDeck(snapshot, plan)
            : 0;
        int act = snapshot["actIndex"]?.GetValue<int>() ?? 0;
        int floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;

        return new MacroResourceSnapshot(
            snapshot["currentHp"]?.GetValue<int>() ?? 0,
            snapshot["maxHp"]?.GetValue<int>() ?? 0,
            snapshot["gold"]?.GetValue<int>() ?? 0,
            snapshot["deck"]?.AsArray()?.Count ?? 0,
            act,
            floor,
            route,
            DescribePhase(act, floor));
    }

    public static ScoringPhaseWeights GetPhaseWeights(JsonObject snapshot) {
        int act = snapshot["actIndex"]?.GetValue<int>() ?? 0;
        int floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;

        if (act == 0 && floor <= 12)
            return new ScoringPhaseWeights(
                1.0f,
                0.45f,
                1.0f,
                "EarlyAct",
                "Prioritize in-run sim (transition); discount option (fight-the-future).");

        if (act >= 2)
            return new ScoringPhaseWeights(
                0.75f,
                1.0f,
                1.1f,
                "LateAct",
                "Deck shape and option value weigh more toward the final boss.");

        return new ScoringPhaseWeights(
            0.9f,
            0.7f,
            1.0f,
            "MidAct",
            "Balanced sim and option; avoid over-thinning.");
    }

    public static DeckComboScore ScoreDeckComposition(JsonObject snapshot, DeckPlan plan) {
        var metrics = DeckEvaluator.Evaluate(snapshot, plan);
        int route = DeckSimScorer.HasRoutePreview(snapshot)
            ? DeckSimScorer.ScoreDeck(snapshot, plan)
            : 0;
        var archetypes = AnalyzeDeckArchetypes(snapshot, plan);

        return new DeckComboScore(
            route,
            DeckEvaluator.DeckQualityScore(metrics, plan),
            metrics.SurvivalGap,
            metrics.ThinGap,
            metrics.StarterBloat,
            archetypes);
    }

    public static CardRoleInsight ClassifyOffer(
        JsonObject card,
        CardOfferBreakdown breakdown,
        JsonObject snapshot,
        DeckPlan plan) {
        var weights = GetPhaseWeights(snapshot);
        var archetypeIds = ComboOptionCatalog.MatchedArchetypeIds(card);
        int inRun = breakdown.Marginal + breakdown.Synergy + breakdown.Early;
        int outRun = breakdown.Option + breakdown.Dilution;

        bool fightFuture = weights.OptionWeight < 0.8f
            && breakdown.Option > Math.Max(4, breakdown.Marginal + 2)
            && breakdown.Marginal <= 6
            && outRun > inRun;

        string role;
        string reason;

        if (fightFuture) {
            role = "FightFuture";
            reason = $"Option({breakdown.Option}) > sim({breakdown.Marginal}) in early act — risks in-run loss.";
        }
        else if (breakdown.Marginal >= 8 && breakdown.Option <= breakdown.Marginal) {
            role = "Transition";
            reason = $"Strong route sim (+{breakdown.Marginal}); low option — front-loaded value.";
        }
        else if (archetypeIds.Any(id => id.Contains("terminal", StringComparison.OrdinalIgnoreCase))) {
            role = "Terminal";
            reason = "Deck-cycle or payoff terminal in combo catalog.";
        }
        else if (archetypeIds.Any(id => id.Contains("enabler") || id.Contains("engine"))) {
            role = archetypeIds.Any(id => id.Contains("engine")) ? "Engine" : "Enabler";
            reason = "Setup piece; option value scales with deck maturity.";
        }
        else if (archetypeIds.Any(id => id.Contains("consumer") || id.Contains("multiplier"))) {
            role = "System";
            reason = "Consumes enabler pieces — archetype payoff card.";
        }
        else if (breakdown.Option >= 6) {
            role = "Option";
            reason = "Future combo value without strong immediate sim.";
        }
        else {
            role = "Neutral";
            reason = "No strong transition or archetype signal.";
        }

        return new CardRoleInsight(role, fightFuture, reason, inRun, outRun, archetypeIds);
    }

    static List<DeckArchetypeInsight> AnalyzeDeckArchetypes(JsonObject snapshot, DeckPlan plan) {
        var deck = snapshot["deck"]?.AsArray();
        var relics = snapshot["relics"]?.AsArray();
        var list = new List<DeckArchetypeInsight>();

        foreach (var archetype in ComboOptionCatalog.Archetypes) {
            int deckPartners = ComboOptionCatalog.CountDeckPartners(deck, archetype.DeckPartners);
            int enablerPieces = ComboOptionCatalog.CountDeckOfferedPieces(deck, archetype.Offered);
            int relicPieces = ComboOptionCatalog.CountRelicPartners(relics, archetype.RelicPartners);
            int pieces = deckPartners + enablerPieces;

            if (pieces == 0 && relicPieces == 0)
                continue;

            float planMul = ComboOptionCatalog.PlanMultiplierPublic(plan, archetype.PlanTags);
            int contrib = (int)Math.Round(
                (Math.Min(pieces, archetype.MaxDeckPartners) * archetype.PerDeckPartner
                + relicPieces * archetype.PerRelicPartner) * planMul);

            list.Add(new DeckArchetypeInsight(
                archetype.Id,
                archetype.Role.ToString(),
                pieces,
                relicPieces,
                contrib));
        }

        list.Sort((a, b) => b.ScoreContribution.CompareTo(a.ScoreContribution));
        return list;
    }

    static string DescribePhase(int act, int floor) {
        if (act == 0 && floor <= 12)
            return "EarlyAct";
        if (act >= 2)
            return "LateAct";
        return "MidAct";
    }
}
