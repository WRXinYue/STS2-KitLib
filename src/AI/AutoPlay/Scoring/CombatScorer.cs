using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Combat;
using DevMode.AI.Combat.Simulation;
using DevMode.AI.Core;
using DevMode.AI.Core.Schema;
using DevMode.AI.Knowledge;

namespace DevMode.AI.AutoPlay.Scoring;

/// <summary>Scores legal combat moves from a JSON snapshot (mechanic-aware heuristics + mod modifiers).</summary>
public static class CombatScorer {
    const int EndTurnBaseScore = -10;

    public static GameAction? PickBestCombatMove(JsonObject snapshot) {
        var ranked = ScoreLegalMovesDetailed(snapshot).ToList();
        var best = ranked.FirstOrDefault();
        return best?.Action;
    }

    public static IEnumerable<CombatMoveScore> ScoreLegalMovesDetailed(JsonObject snapshot) {
        var combat = snapshot["combat"]?.AsObject();
        if (combat == null) {
            yield return BuildEndTurn("No combat", EndTurnBaseScore, snapshot);
            yield break;
        }

        if (!HasAliveEnemy(combat)) {
            yield return BuildEndTurn("No enemies", EndTurnBaseScore, snapshot);
            yield break;
        }

        var hand = combat["hand"]?.AsArray();
        var energy = combat["currentEnergy"]?.GetValue<int>() ?? 0;
        var hpRatio = IntentCalculator.HpRatio(snapshot);
        var lowHp = hpRatio < 0.4f;
        var needsBlock = IntentCalculator.NeedsBlock(snapshot);
        var canLethal = LethalChecker.CanLethal(snapshot, out _);
        var canSecureKill = BlockDefensePolicy.CanSkipBlockForKill(snapshot);
        var simState = CombatState.FromSnapshot(snapshot);
        var incoming = IntentCalculator.TotalIncomingDamage(snapshot);
        var netIncoming = IntentCalculator.NetDamageAfterBlock(snapshot);
        var enemies = combat["enemies"]?.AsArray();
        var multiEnemy = IntentCalculator.AliveEnemyCount(snapshot) >= 2;

        if (hand != null) {
            for (var i = 0; i < hand.Count; i++) {
                var card = hand[i]?.AsObject();
                if (card == null) continue;
                if (card["canPlay"]?.GetValue<bool>() == false) continue;

                var cost = card["cost"]?.GetValue<int>() ?? 99;
                if (cost > energy) continue;

                var targetType = card["targetType"]?.GetValue<string>() ?? "";
                var profile = CombatCardStats.ResolveProfile(card);
                if (CombatTargetTypes.IsAnyEnemy(targetType)
                    || CombatTargetTypes.AppliesDirectedDebuff(profile)) {
                    var targetIndices = EnemyIndexResolver.ViableCombatIndices(enemies).ToList();
                    if (targetIndices.Count == 0)
                        targetIndices.Add(0);

                    foreach (var combatIndex in targetIndices) {
                        var move = PlayCard(i, combatIndex, card);
                        yield return ScoreCardDetailed(snapshot, simState, move, card, hand, energy,
                            lowHp, hpRatio, needsBlock, canLethal, canSecureKill, incoming, netIncoming,
                            multiEnemy, enemies, combatIndex);
                    }
                }
                else {
                    var move = PlayCard(i, -1, card);
                    yield return ScoreCardDetailed(snapshot, simState, move, card, hand, energy,
                        lowHp, hpRatio, needsBlock, canLethal, canSecureKill, incoming, netIncoming,
                        multiEnemy, enemies, -1);
                }
            }
        }

        yield return BuildEndTurn("End turn", ScoreEndTurn(needsBlock, incoming, snapshot), snapshot);
    }

    public static IEnumerable<(GameAction Action, int Score)> ScoreLegalMoves(JsonObject snapshot) {
        foreach (var scored in ScoreLegalMovesDetailed(snapshot))
            yield return (scored.Action, scored.Score);
    }

    static CombatMoveScore ScoreCardDetailed(
        JsonObject snapshot,
        CombatState simState,
        GameAction move,
        JsonObject card,
        JsonArray? hand,
        int energy,
        bool lowHp,
        float hpRatio,
        bool needsBlock,
        bool canLethal,
        bool canSecureKill,
        int incoming,
        int netIncoming,
        bool multiEnemy,
        JsonArray? enemies,
        int targetIndex) {
        var profile = CombatCardStats.ResolveProfile(card);
        var cost = card["cost"]?.GetValue<int>() ?? 1;
        var damageValue = CombatCardStats.ResolveDamage(card);
        var blockValue = CombatCardStats.ResolveBlock(card);
        var cardId = card["id"]?.GetValue<string>() ?? "";
        var isAttack = CombatCardStats.IsAttackCard(card);
        var isSkill = CombatCardStats.IsSkillCard(card);
        var hasBlock = blockValue > 0;
        var targetEnemy = ResolveTargetEnemy(enemies, targetIndex);

        var tags = CardCatalog.ResolveTags(
            cardId,
            card["cardType"]?.GetValue<string>(),
            card["keywords"]?.AsArray());
        var isAoe = tags.Contains(AiTag.Aoe)
            || CombatTargetTypes.IsAllEnemies(card["targetType"]?.GetValue<string>());
        var isBlockCard = hasBlock || tags.Contains(AiTag.Block);
        var blockUrgency = IntentCalculator.BlockUrgency(snapshot);
        var fatalIfUnblocked = IntentCalculator.IsFatalIfUnblocked(snapshot);

        var builder = new ScoreBuilder();
        var isJunkPlay = CombatJunkCard.IsJunkId(cardId, card["rarity"]?.GetValue<string>())
            || string.Equals(card["cardType"]?.GetValue<string>(), "Status", StringComparison.OrdinalIgnoreCase);
        if (isJunkPlay) {
            if (hand != null && MechanicCombatBonus.HandHasEmergencyJunkClearForScorer(hand, card))
                builder.Add("emergency-junk", HandHasAffordableAttack(hand) ? 8 : 14);
            else
                builder.Add("junk-play", -200);
        }

        var shouldScoreBlock = BlockThreatEvaluator.ShouldScoreBlock(snapshot);
        var suppressTransform = BlockThreatEvaluator.ShouldSuppressTransform(snapshot);

        if ((needsBlock || shouldScoreBlock) && (isSkill || isBlockCard) && hasBlock) {
            var blockNeeded = Math.Max(0, netIncoming);
            if (blockNeeded > 0) {
                var effectiveBlock = Math.Min(blockValue, blockNeeded);
                var blockBase = needsBlock ? 25 : 15;
                builder.Add("block", blockBase + effectiveBlock * 2);
                if (incoming >= 15) builder.Add("big-hit", 12);
                if (fatalIfUnblocked) builder.Add("fatal", 25);
                if (blockValue > blockNeeded + 5)
                    builder.Add("overblock", -(blockValue - blockNeeded));
            }
        }
        else if ((isSkill || isBlockCard) && !shouldScoreBlock && !MechanicCombatBonus.IsSetupSkill(profile)) {
            var pileDraw = CardPileEffectResolver.DrawCount(cardId);
            var exhaustHand = CardPileEffectResolver.ExhaustHandCount(cardId);
            if (pileDraw <= 0 && exhaustHand <= 0
                && !profile.Flags.HasFlag(CardMechanicFlags.HasDraw)
                && !profile.Flags.HasFlag(CardMechanicFlags.HasExhaustFromHand))
                builder.Add("skill-no-block-need", -CombatScoreWeights.NonSetupSkillPenalty);
        }

        if (hasBlock && incoming > 0
            && BlockThreatEvaluator.IsStarterDefend(cardId, card["rarity"]?.GetValue<string>())) {
            builder.Add("starter-block", 8);
        }

        if (lowHp && (isSkill || isBlockCard) && needsBlock)
            builder.Add("low-hp-skill", 20);

        if (IsSelfDamageCard(cardId) && hpRatio < 0.65f
            && !(hasBlock && needsBlock && blockValue >= netIncoming))
            builder.Add("self-dmg", -30);

        if (isAttack) {
            builder.Add("attack", 20 + cost * 5 + damageValue);
            builder.Add("target", TargetEnemyBonus(enemies, targetIndex, incoming));

            var thisCardKills = targetIndex >= 0
                && SimLethalChecker.CanKillEnemyThisAction(simState, move.TargetIndex, targetIndex);

            if (needsBlock) {
                builder.Add("attack-unsafe", -(blockUrgency / 2 + Math.Max(0, netIncoming - damageValue) / 2));
                if (thisCardKills && (canSecureKill || netIncoming <= 0))
                    builder.Add("lethal", 25);
                else if (thisCardKills && !fatalIfUnblocked)
                    builder.Add("lethal-risky", 8);
            }
            else {
                if (shouldScoreBlock && netIncoming > 0)
                    builder.Add("attack-unsafe", -netIncoming / 3);

                if (thisCardKills || (canLethal && canSecureKill))
                    builder.Add("lethal", 25);
                else if (canLethal)
                    builder.Add("lethal-risky", 8);
            }

            if (CombatTransformSimulator.IsTransformableAttack(card)
                && BlockThreatEvaluator.HasAffordableHandTransform(hand, energy)) {
                var (transformCard, _) = BlockThreatEvaluator.FindAffordableHandTransform(hand, energy);
                if (transformCard != null) {
                    var gain = BlockThreatEvaluator.TransformDamageGain(hand, transformCard);
                    builder.Add("attack-before-transform",
                        -Math.Min(gain, CombatScoreWeights.AttackBeforeTransformCap));
                }
            }

            var deferCost = CombatSetupEvaluator.ComputeVulnerableDeferOpportunityCost(
                snapshot, hand, energy, targetEnemy, damageValue);
            if (deferCost > 0)
                builder.Add("defer-vuln", -deferCost);
        }
        else if (isSkill)
            builder.Add("skill", 15 + cost * 2 + (needsBlock ? blockValue : 0));
        else
            builder.Add("other", 10);

        if (isAoe && multiEnemy) builder.Add("aoe", 12);
        if (multiEnemy && isAttack) builder.Add("multi", 15);
        if (multiEnemy && hasBlock && !needsBlock) builder.Add("multi-defend", -20);

        builder.Add("cost", -Math.Max(0, cost - 1) * 2);

        var mechBonus = MechanicCombatBonus.Score(
            snapshot, card, profile, hand, targetEnemy, energy, suppressTransform);

        if ((profile.AppliedVulnerable > 0 || profile.Flags.HasFlag(CardMechanicFlags.AppliesVulnerable))
            && CombatPowerReader.GetVulnerable(targetEnemy) <= 0) {
            var stacks = Math.Max(profile.AppliedVulnerable, 1);
            var deferSetup = CombatSetupEvaluator.ComputeVulnerableDeferValue(
                snapshot, hand, energy, targetEnemy, stacks, cost,
                move.TargetIndex, card);
            if (deferSetup > 0)
                builder.Add("defer-vuln-setup", deferSetup);
        }

        if (suppressTransform
            && profile.Flags.HasFlag(CardMechanicFlags.TransformsHandAttacks)
            && !(canSecureKill && !fatalIfUnblocked)) {
            var discount = (int)Math.Round(
                CombatScoreWeights.TransformThreatDiscountMax * blockUrgency / 100f);
            if (discount > 0)
                builder.Add("transform-threat-discount", -discount);
        }
        else if (needsBlock && blockUrgency >= 40 && (isAttack || MechanicCombatBonus.IsSetupSkill(profile))) {
            mechBonus = mechBonus * 2 / 3;
        }
        builder.Add("mechanic", mechBonus);

        var cardName = card["name"]?.GetValue<string>() ?? cardId;
        var withMods = AiMoveModifierHub.ApplyModifiers(snapshot, move, builder.Total);
        if (withMods != builder.Total)
            builder.Add("mod", withMods - builder.Total);

        return builder.Build(move with {
            Reason = $"{cardName} score={withMods} [{builder.FormatTerms()}]",
        });
    }

    static int ScoreEndTurn(bool needsBlock, int incoming, JsonObject snapshot) {
        var score = EndTurnBaseScore;
        if (needsBlock && incoming > 0)
            score -= 15 + IntentCalculator.BlockUrgency(snapshot) / 4;

        if (incoming == 0) {
            var next = ThreatModel.NextTurnIncoming(CombatState.FromSnapshot(snapshot));
            if (next >= 8)
                score -= next / 2;
        }

        var combat = snapshot["combat"]?.AsObject();
        var enemies = combat?["enemies"]?.AsArray();
        if (enemies != null) {
            foreach (var node in enemies) {
                if (node is not JsonObject enemy) continue;
                if (enemy["isAlive"]?.GetValue<bool>() == false) continue;
                var vuln = CombatPowerReader.GetVulnerable(enemy);
                if (vuln > 0) {
                    score += Math.Min(15, vuln * 3);
                    break;
                }
            }
        }

        return score;
    }

    static CombatMoveScore BuildEndTurn(string reason, int baseScore, JsonObject snapshot) {
        var action = new GameAction { Type = ActionType.EndTurn, Reason = reason };
        var score = AiMoveModifierHub.ApplyModifiers(snapshot, action, baseScore);
        var terms = new List<string> { $"base:{baseScore}" };
        if (score != baseScore)
            terms.Add($"mod:{score - baseScore:+0;-0;0}");
        return new CombatMoveScore(action with { Reason = $"EndTurn score={score}" }, score, terms);
    }

    static JsonObject? ResolveTargetEnemy(JsonArray? enemies, int combatIndex) =>
        EnemyIndexResolver.FindByCombatIndex(enemies, combatIndex);

    static bool IsSelfDamageCard(string cardId) {
        var upper = cardId.ToUpperInvariant();
        return upper.Contains("HEMOKINESIS") || upper.Contains("BLOODLETTING")
            || upper.Contains("BLOOD_WALL") || upper.Contains("OFFERING");
    }

    static int TargetEnemyBonus(JsonArray? enemies, int targetIndex, int incoming) {
        if (enemies == null || enemies.Count == 0)
            return 0;

        JsonObject? target = ResolveTargetEnemy(enemies, targetIndex);
        if (target == null) return 0;

        if (LethalExclusions.ShouldSkip(target)
            && enemies.Any(e => e is JsonObject o
                && o["isAlive"]?.GetValue<bool>() != false
                && !LethalExclusions.ShouldSkip(o)))
            return -60;

        var hp = target["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = target["maxHp"]?.GetValue<int>() ?? 1;
        var lowEnemy = maxHp > 0 && hp <= maxHp * 0.25;
        var bonus = lowEnemy ? 30 : 5;
        var arraySlot = EnemyIndexResolver.ArraySlot(enemies, targetIndex);
        bonus += EnemyTargetPriority.TargetBias(
            enemies, arraySlot >= 0 ? arraySlot : targetIndex);

        if (incoming == 0) {
            var steps = target["intentSteps"]?.AsArray();
            if (steps != null && steps.Count > 1
                && steps[1] is JsonObject nextStep) {
                var nextAtk = nextStep["intentDamage"]?.GetValue<int>() ?? 0;
                if (nextAtk >= 6)
                    bonus += Math.Min(40, nextAtk * 3);
            }

            var monsterId = target["monsterId"]?.GetValue<string>();
            var moveId = target["nextMoveId"]?.GetValue<string>();
            foreach (var effect in MoveEffectIndex.GetEffects(monsterId, moveId)) {
                if (effect.Kind == MonsterMoveEffectKind.StatusInject)
                    bonus += effect.Count * 2;
                if (effect.Kind == MonsterMoveEffectKind.Summon)
                    bonus += 12;
            }
        }

        return bonus;
    }

    static bool HasAliveEnemy(JsonObject combat) {
        var enemies = combat["enemies"]?.AsArray();
        if (enemies == null) return true;
        if (enemies.Count == 0) return false;
        return enemies.Any(e => e?["isAlive"]?.GetValue<bool>() != false);
    }

    static GameAction PlayCard(int handIndex, int targetIndex, JsonObject card) => new() {
        Type = ActionType.PlayCard,
        TargetIndex = handIndex,
        SecondaryIndex = targetIndex,
        Reason = card["name"]?.GetValue<string>() ?? card["id"]?.GetValue<string>() ?? "?",
    };

    static bool HandHasAffordableAttack(JsonArray? hand) {
        if (hand == null) return false;
        foreach (var node in hand) {
            if (node is not JsonObject card) continue;
            if (!CombatCardStats.IsAttackCard(card)) continue;
            if (CombatCardStats.ResolveDamage(card) <= 0) continue;
            if (card["canPlay"]?.GetValue<bool>() == false) continue;
            return true;
        }
        return false;
    }
}
