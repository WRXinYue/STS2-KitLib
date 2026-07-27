using System.Linq;
using KitLib.AI.Combat;
using KitLib.AI.Combat.Simulation;

namespace KitLib.AI.Tests.Combat;

/// <summary>
/// Offline sandbox for SHRINKER_BEETLE turn-1 openings (no game process).
/// Uses official Ironclad starter IDs from ModelDb when available.
/// </summary>
public sealed class ShrinkerBeetleTurn1SandboxTests : ModelDbTestBase {
    const string BashId = OfficialIroncladCards.Bash;
    const string StrikeId = OfficialIroncladCards.Strike;
    const string DefendId = OfficialIroncladCards.Defend;

    [Fact]
    public void OfficialModelDb_StarterStats_MatchSandboxCatalog() {
        OfficialCombatSimBootstrap.VerifyStarterStatsMatchSandbox();
    }

    [Fact]
    public void ShrinkerBeetleTurn1_BashThenStrike_BeatsTripleStrike_InLineRanking() {
        var root = CombatScenarioFactory.ShrinkerBeetleTurn1([
            StrikeId,
            StrikeId,
            StrikeId,
            BashId,
            DefendId,
        ]);

        var ranked = CombatLineSandbox.RankCompleteLines(root);
        var report = CombatLineSandbox.FormatRankingReport(ranked);

        var bashStrike = ranked.FirstOrDefault(l =>
            l.Label.Contains(BashId, System.StringComparison.OrdinalIgnoreCase)
            && l.Label.Contains(StrikeId, System.StringComparison.OrdinalIgnoreCase)
            && !l.Label.Contains(DefendId, System.StringComparison.OrdinalIgnoreCase));

        var tripleStrike = ranked.FirstOrDefault(l =>
            l.Label.Split('>').Count(s => s.StartsWith(StrikeId, System.StringComparison.OrdinalIgnoreCase)) >= 3);

        Assert.NotNull(bashStrike);
        Assert.NotNull(tripleStrike);

        int cmp = CombatSetupEvaluator.CompareLines(tripleStrike!.Outcome, bashStrike!.Outcome);
        Assert.True(
            cmp > 0,
            $"Expected {bashStrike.Label} to beat {tripleStrike.Label} under CompareLines.\n{report}");
    }

    [Fact]
    public void ShrinkerBeetleTurn1_BashThenStrike_BeatsBashThenDefend_InLineRanking() {
        var root = CombatScenarioFactory.ShrinkerBeetleTurn1([
            StrikeId,
            StrikeId,
            StrikeId,
            BashId,
            DefendId,
        ]);

        var ranked = CombatLineSandbox.RankCompleteLines(root);
        var report = CombatLineSandbox.FormatRankingReport(ranked);

        var bashStrike = ranked.FirstOrDefault(l =>
            l.Label.Contains(BashId, System.StringComparison.OrdinalIgnoreCase)
            && l.Label.Contains(StrikeId, System.StringComparison.OrdinalIgnoreCase)
            && !l.Label.Contains(DefendId, System.StringComparison.OrdinalIgnoreCase));

        var bashDefend = ranked.FirstOrDefault(l =>
            l.Label.Contains(BashId, System.StringComparison.OrdinalIgnoreCase)
            && l.Label.Contains(DefendId, System.StringComparison.OrdinalIgnoreCase)
            && !l.Label.Contains(StrikeId, System.StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(bashStrike);
        Assert.NotNull(bashDefend);

        int cmp = CombatSetupEvaluator.CompareLines(bashDefend!.Outcome, bashStrike!.Outcome);
        Assert.True(
            cmp > 0,
            $"Expected {bashStrike.Label} to beat {bashDefend.Label} under CompareLines.\n{report}");
    }

    [Fact]
    public void ShrinkerBeetleTurn1_BashThenStrike_IsTopRankedLine() {
        var root = CombatScenarioFactory.ShrinkerBeetleTurn1([
            StrikeId,
            StrikeId,
            StrikeId,
            BashId,
            DefendId,
        ]);

        var ranked = CombatLineSandbox.RankCompleteLines(root);
        var top = ranked[0];
        var report = CombatLineSandbox.FormatRankingReport(ranked, 12);

        Assert.DoesNotContain(DefendId, top.Label, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(BashId, top.Label, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(StrikeId, top.Label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShrinkerBeetleTurn1_ReportsLineMetrics_ForManualReview() {
        var root = CombatScenarioFactory.ShrinkerBeetleTurn1([
            StrikeId,
            StrikeId,
            StrikeId,
            BashId,
            DefendId,
        ]);

        var ranked = CombatLineSandbox.RankCompleteLines(root);
        var candidates = new[] {
            $"{StrikeId}→e0>{BashId}→e0",
        };

        foreach (var prefix in candidates) {
            var line = CombatLineSandbox.FindByLabel(ranked, prefix);
            Assert.NotNull(line);
        }

        Assert.True(
            ranked.Any(l => l.Label.Split('>').Count(s =>
                s.StartsWith(StrikeId, StringComparison.OrdinalIgnoreCase)) >= 3),
            CombatLineSandbox.FormatRankingReport(ranked));

        Assert.True(ranked.Count >= 8, CombatLineSandbox.FormatRankingReport(ranked));
    }
}
