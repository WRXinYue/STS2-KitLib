using System.Linq;
using KitLib.AI.Combat;
using KitLib.AI.Combat.Simulation;
using MegaCrit.Sts2.Core.Models.Monsters;

namespace KitLib.AI.Tests.Combat;

/// <summary>
/// Multi-turn SHRINKER_BEETLE fight sandbox: official stats, shrink debuff, T2 chomp defense.
/// </summary>
public sealed class ShrinkerBeetleFightSandboxTests : ModelDbTestBase {
    const string BashId = OfficialIroncladCards.Bash;
    const string StrikeId = OfficialIroncladCards.Strike;
    const string DefendId = OfficialIroncladCards.Defend;

    static readonly string[] DefaultOpeningHand = [
        StrikeId, StrikeId, StrikeId, BashId, DefendId,
    ];

    [Fact]
    public void OfficialModelDb_ShrinkerBeetleStats_MatchSandbox() {
        OfficialCombatSimBootstrap.EnsureReady();
        if (!OfficialCombatSimBootstrap.IsReady)
            return;

        try {
            var beetle = MegaCrit.Sts2.Core.Models.ModelDb.Monster<ShrinkerBeetle>();
            Assert.InRange(beetle.MinInitialHp, 38, 40);
            Assert.InRange(beetle.MaxInitialHp, 40, 42);
        }
        finally {
            OfficialCombatSimBootstrap.ResetForTest();
        }
    }

    [Fact]
    public void Turn1_EndTurn_AppliesShrinkModifier() {
        var root = CombatScenarioFactory.ShrinkerBeetleIroncladFight(DefaultOpeningHand);
        var path = CombatLineSandbox.BuildPlayPath(root, StrikeId, BashId);
        var leaf = CombatLineSandbox.ApplyPath(root, path);
        var afterTurn = CombatTurnResolver.ResolveEndTurn(leaf);

        Assert.Contains(
            afterTurn.Modifiers,
            m => m.PowerId.Contains("SHRINK", StringComparison.OrdinalIgnoreCase));

        var enemy = afterTurn.Enemies[0];
        Assert.Equal("CHOMP_MOVE", enemy.NextMoveId);
        Assert.Equal(8, enemy.IntentDamage);

        int strikeIdx = -1;
        for (int i = 0; i < afterTurn.Hand.Count; i++) {
            if (string.Equals(afterTurn.Hand[i].Id, StrikeId, StringComparison.OrdinalIgnoreCase)) {
                strikeIdx = i;
                break;
            }
        }

        if (strikeIdx >= 0) {
            int shrunkStrike = CombatDamageCalc.OutgoingDamage(
                afterTurn.Hand[strikeIdx], afterTurn, vulnerableOnTarget: 0);
            Assert.Equal(4, shrunkStrike);

            int vulnShrunkStrike = CombatDamageCalc.OutgoingDamage(
                afterTurn.Hand[strikeIdx], afterTurn, enemy.Vulnerable);
            Assert.Equal(6, vulnShrunkStrike);
        }
    }

    [Fact]
    public void Turn2_WithIncomingChomp_DefendThenStrike_BeatsTripleDefend() {
        var root = CombatScenarioFactory.ShrinkerBeetleIroncladFight(DefaultOpeningHand);
        var t1Path = CombatLineSandbox.BuildPlayPath(root, StrikeId, BashId);
        var afterT1 = CombatTurnResolver.ResolveEndTurn(CombatLineSandbox.ApplyPath(root, t1Path));

        Assert.True(ThreatModel.IncomingDamage(afterT1) > 0, "T2 should have chomp incoming.");
        Assert.Equal(8, afterT1.Enemies[0].IntentDamage);

        var ranked = CombatLineSandbox.RankCompleteLines(afterT1);
        var report = CombatLineSandbox.FormatRankingReport(ranked, 12);

        var tripleDefend = ranked.FirstOrDefault(l =>
            !l.Label.Contains(StrikeId, StringComparison.OrdinalIgnoreCase)
            && l.Label.Split('>').Count(s =>
                s.StartsWith(DefendId, StringComparison.OrdinalIgnoreCase)) >= 3);

        var defendStrike = ranked.FirstOrDefault(l =>
            l.Label.Contains(DefendId, StringComparison.OrdinalIgnoreCase)
            && l.Label.Contains(StrikeId, StringComparison.OrdinalIgnoreCase));

        Assert.True(tripleDefend != null, report);
        Assert.True(defendStrike != null, report);

        int cmp = CombatSetupEvaluator.CompareLines(tripleDefend!.Outcome, defendStrike!.Outcome);
        Assert.True(
            cmp > 0,
            $"Expected {defendStrike.Label} to beat {tripleDefend.Label} on T2 (chomp=8).\n{report}");
    }

    [Fact]
    public void Turn1_OpeningRanking_Report_ForManualReview() {
        var root = CombatScenarioFactory.ShrinkerBeetleIroncladFight(DefaultOpeningHand);
        var ranked = CombatLineSandbox.RankCompleteLines(root);
        var report = CombatLineSandbox.FormatRankingReport(ranked, 8);

        Assert.True(ranked.Count >= 8, report);
        Assert.Contains(BashId, ranked[0].Label, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(DefendId, ranked[0].Label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VanillaWeak_Encounter_Uses7Chomp13Stomp_InSandbox() {
        var root = CombatScenarioFactory.ShrinkerBeetleIroncladFight(
            DefaultOpeningHand,
            deadlyEnemies: false);
        var enemy = root.Enemies[0];

        Assert.Equal(7, enemy.IntentSteps[1].IntentDamage);
        Assert.Equal(13, enemy.IntentSteps[2].IntentDamage);
    }

    [Fact]
    public void AnchorRelic_GrantsStartBlock_InSandbox() {
        var noRelic = CombatScenarioFactory.ShrinkerBeetleIroncladFight(DefaultOpeningHand);
        var withAnchor = CombatScenarioFactory.ShrinkerBeetleIroncladFight(
            DefaultOpeningHand,
            relicIds: ["ANCHOR"]);

        Assert.Equal(0, noRelic.PlayerBlock);
        Assert.Equal(10, withAnchor.PlayerBlock);
    }

    [Fact]
    public void UnsupportedRelics_AreNotModeled_InSandbox() {
        var noRelic = CombatScenarioFactory.ShrinkerBeetleIroncladFight(DefaultOpeningHand);
        var withVambrace = CombatScenarioFactory.ShrinkerBeetleIroncladFight(
            DefaultOpeningHand,
            relicIds: ["VAMBRACE"]);

        Assert.Equal(0, noRelic.PlayerBlock);
        Assert.Equal(0, withVambrace.PlayerBlock);
    }
}
