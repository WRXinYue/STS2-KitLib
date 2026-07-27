using System;
using System.Text.Json.Nodes;
using KitLib.AI.AutoPlay.Scoring;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Planning;

public sealed record CardOfferBreakdown(
    int Marginal,
    int Synergy,
    int Option,
    int Dilution,
    int Early,
    int Codex,
    int NextFight,
    float ExerciseProb) {
    public int Total => Marginal + Synergy + Option + Dilution + Early;
}

/// <summary>Public card-offer scoring for autoplay and Dev Viewer (current + option + dilution).</summary>
public static class CardOfferScoring {
    public static CardOfferBreakdown ScoreBreakdown(
        JsonObject card,
        DeckPlan plan,
        int deckSize,
        JsonObject? snapshot = null) {
        if (snapshot == null) {
            int absolute = ScoreAbsolute(card, plan, deckSize);
            return new CardOfferBreakdown(absolute, 0, 0, 0, 0, 0, 0, 0f);
        }

        var metrics = DeckEvaluator.Evaluate(snapshot, plan);
        var deck = snapshot["deck"]?.AsArray();

        int marginal = DeckEvaluator.MarginalPickScore(snapshot, plan, card);
        int synergy = DeckSynergyEvaluator.ScoreCard(card, plan, snapshot);
        float exercise = ComboExerciseEstimator.EstimateForOffer(card, snapshot);
        int option = ComboOptionCatalog.ScoreCard(card, plan, snapshot, exercise);
        int dilution = DeckSynergyEvaluator.ScoreDeckDilutionOffer(card, plan, metrics, deck);
        int early = EarlyCardRewardAdjustments.Score(card, snapshot);
        int codex = MacroScorerHelper.ScaledCodexBonus(card, snapshot, metrics);
        int nextFight = DeckSimScorer.HasRoutePreview(snapshot)
            ? marginal
            : DeckSimScorer.MarginalCardDelta(snapshot, card, plan);

        return new CardOfferBreakdown(
            marginal, synergy, option, dilution, early, codex, nextFight, exercise);
    }

    public static int ScoreTotal(JsonObject card, DeckPlan plan, int deckSize, JsonObject? snapshot = null) =>
        ScoreBreakdown(card, plan, deckSize, snapshot).Total;

    static int ScoreAbsolute(JsonObject card, DeckPlan plan, int deckSize) {
        var composition = new DeckComposition(0, 0, 0, 0);
        var score = DeckCardScoring.ScoreInDeck(card, plan, composition);
        score -= (int)Math.Round(DeckPlanInferer.DilutionPenalty(deckSize + 1, plan));
        return score;
    }
}
