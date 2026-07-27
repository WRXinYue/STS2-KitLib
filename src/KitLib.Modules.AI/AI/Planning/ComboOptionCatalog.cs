using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Planning;

/// <summary>Combo role for option-value scoring (future payoff when partners arrive or mature).</summary>
public enum ComboRole {
    Enabler,
    Consumer,
    Terminal,
    Engine,
    Multiplier,
}

public enum ComboPartnerKind {
    MechanicFlag,
    AiTag,
    CardId,
    IdContains,
    InstallsPower,
    HitScalePlayerBlock,
    RelicId,
    RelicTag,
}

public readonly record struct ComboPartnerSpec(ComboPartnerKind Kind, string Token);

/// <summary>
/// Enabler/consumer chains and plan maturity for card option value (distinct from immediate sim marginal).
/// </summary>
public static class ComboOptionCatalog {
    public static readonly ComboArchetype[] Archetypes = [
        new ComboArchetype(
            "vuln_enabler",
            ComboRole.Enabler,
            new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.AppliesVulnerable)),
            [
                new ComboPartnerSpec(ComboPartnerKind.IdContains, "RIP"),
                new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.HasDamage)),
            ],
            [
                new ComboPartnerSpec(ComboPartnerKind.RelicId, "KUNAI"),
                new ComboPartnerSpec(ComboPartnerKind.RelicId, "SHURIKEN"),
            ],
            [AiTag.Attack],
            6, 5, 4, 4),
        new ComboArchetype(
            "vuln_consumer",
            ComboRole.Consumer,
            new ComboPartnerSpec(ComboPartnerKind.IdContains, "RIP"),
            [
                new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.AppliesVulnerable)),
                new ComboPartnerSpec(ComboPartnerKind.IdContains, "BASH"),
                new ComboPartnerSpec(ComboPartnerKind.IdContains, "BREAK"),
            ],
            [],
            [AiTag.Attack],
            5, 6, 0, 3),
        new ComboArchetype(
            "block_scale_enabler",
            ComboRole.Enabler,
            new ComboPartnerSpec(ComboPartnerKind.IdContains, "BLOOD_WALL"),
            [
                new ComboPartnerSpec(ComboPartnerKind.HitScalePlayerBlock, ""),
                new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.HasBlock)),
                new ComboPartnerSpec(ComboPartnerKind.IdContains, "BARRICADE"),
            ],
            [
                new ComboPartnerSpec(ComboPartnerKind.RelicId, "VAMBRACE"),
                new ComboPartnerSpec(ComboPartnerKind.RelicTag, nameof(AiTag.Block)),
            ],
            [AiTag.Block, AiTag.Attack],
            7, 5, 5, 4),
        new ComboArchetype(
            "block_scale_consumer",
            ComboRole.Consumer,
            new ComboPartnerSpec(ComboPartnerKind.HitScalePlayerBlock, ""),
            [
                new ComboPartnerSpec(ComboPartnerKind.IdContains, "BLOOD_WALL"),
                new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.HasBlock)),
                new ComboPartnerSpec(ComboPartnerKind.IdContains, "BARRICADE"),
                new ComboPartnerSpec(ComboPartnerKind.InstallsPower, nameof(PlayerPowerEffectKind.Dexterity)),
            ],
            [
                new ComboPartnerSpec(ComboPartnerKind.RelicId, "VAMBRACE"),
                new ComboPartnerSpec(ComboPartnerKind.RelicTag, nameof(AiTag.Block)),
            ],
            [AiTag.Block, AiTag.Attack],
            6, 6, 5, 3),
        new ComboArchetype(
            "block_retain_setup",
            ComboRole.Enabler,
            new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.Retain)),
            [
                new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.HasBlock)),
                new ComboPartnerSpec(ComboPartnerKind.HitScalePlayerBlock, ""),
            ],
            [
                new ComboPartnerSpec(ComboPartnerKind.RelicId, "VAMBRACE"),
                new ComboPartnerSpec(ComboPartnerKind.RelicTag, nameof(AiTag.Block)),
            ],
            [AiTag.Block, AiTag.Setup],
            5, 4, 4, 3),
        new ComboArchetype(
            "exhaust_engine",
            ComboRole.Engine,
            new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.HasExhaustFromHand)),
            [
                new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.Exhaust)),
                new ComboPartnerSpec(ComboPartnerKind.IdContains, "FEEL_NO_PAIN"),
            ],
            [
                new ComboPartnerSpec(ComboPartnerKind.RelicId, "DEAD_BRANCH"),
                new ComboPartnerSpec(ComboPartnerKind.RelicTag, nameof(AiTag.Exhaust)),
            ],
            [AiTag.Exhaust],
            6, 5, 4, 5),
        new ComboArchetype(
            "exhaust_terminal",
            ComboRole.Terminal,
            new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.Exhaust)),
            [
                new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.HasExhaustFromHand)),
                new ComboPartnerSpec(ComboPartnerKind.IdContains, "FEEL_NO_PAIN"),
                new ComboPartnerSpec(ComboPartnerKind.InstallsPower, nameof(PlayerPowerEffectKind.Strength)),
            ],
            [
                new ComboPartnerSpec(ComboPartnerKind.RelicId, "DEAD_BRANCH"),
            ],
            [AiTag.Exhaust, AiTag.Scaling],
            5, 5, 4, 4),
        new ComboArchetype(
            "strength_multiplier",
            ComboRole.Multiplier,
            new ComboPartnerSpec(ComboPartnerKind.InstallsPower, nameof(PlayerPowerEffectKind.Strength)),
            [
                new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.Exhaust)),
                new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.HasDamage)),
                new ComboPartnerSpec(ComboPartnerKind.IdContains, "FEEL_NO_PAIN"),
            ],
            [
                new ComboPartnerSpec(ComboPartnerKind.RelicId, "FROZEN_EYE"),
            ],
            [AiTag.Scaling, AiTag.Attack],
            5, 4, 3, 4),
        new ComboArchetype(
            "draw_engine",
            ComboRole.Engine,
            new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.HasDraw)),
            [
                new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.HasDamage)),
                new ComboPartnerSpec(ComboPartnerKind.AiTag, nameof(AiTag.Attack)),
            ],
            [
                new ComboPartnerSpec(ComboPartnerKind.RelicTag, nameof(AiTag.Draw)),
            ],
            [AiTag.Draw, AiTag.Attack],
            5, 4, 3, 5),
        new ComboArchetype(
            "draw_terminal",
            ComboRole.Terminal,
            new ComboPartnerSpec(ComboPartnerKind.IdContains, "HILT"),
            [
                new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.HasDraw)),
                new ComboPartnerSpec(ComboPartnerKind.AiTag, nameof(AiTag.Draw)),
            ],
            [
                new ComboPartnerSpec(ComboPartnerKind.RelicTag, nameof(AiTag.Draw)),
            ],
            [AiTag.Draw],
            8, 5, 4, 3),
        new ComboArchetype(
            "transform_enabler",
            ComboRole.Enabler,
            new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.TransformsHandAttacks)),
            [
                new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.HasDamage)),
                new ComboPartnerSpec(ComboPartnerKind.AiTag, nameof(AiTag.Attack)),
            ],
            [],
            [AiTag.Attack, AiTag.Setup],
            7, 4, 0, 6),
        new ComboArchetype(
            "transform_consumer",
            ComboRole.Consumer,
            new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.TransformsCards)),
            [
                new ComboPartnerSpec(ComboPartnerKind.MechanicFlag, nameof(CardMechanicFlags.HasDamage)),
                new ComboPartnerSpec(ComboPartnerKind.IdContains, "STRIKE"),
            ],
            [],
            [AiTag.Attack],
            6, 4, 0, 5),
    ];

    public static int ScoreCard(JsonObject card, DeckPlan plan, JsonObject? snapshot) =>
        ScoreCard(card, plan, snapshot, 0.35f);

    public static int ScoreCard(
        JsonObject card,
        DeckPlan plan,
        JsonObject? snapshot,
        float exerciseProb) {
        if (snapshot == null)
            return 0;

        var profile = ResolveProfile(card);
        var deck = snapshot["deck"]?.AsArray();
        var relics = snapshot["relics"]?.AsArray();
        int actIndex = snapshot["actIndex"]?.GetValue<int>() ?? 0;

        int total = 0;
        foreach (var archetype in Archetypes) {
            if (!MatchesOffered(card, profile, archetype.Offered))
                continue;

            int deckPartners = CountDeckPartners(deck, archetype.DeckPartners);
            int relicPartners = CountRelicPartners(relics, archetype.RelicPartners);
            float planMul = PlanMultiplier(plan, archetype.PlanTags);
            float maturity = MaturityMultiplier(
                deckPartners, relicPartners, archetype.MaxDeckPartners, archetype.Role);
            float earlyMul = EarlyActMultiplier(archetype.Role, actIndex, deckPartners + relicPartners);
            float exerciseMul = deckPartners + relicPartners > 0
                ? 1f
                : 0.55f + 0.45f * Math.Clamp(exerciseProb, 0f, 1f);

            int deckBonus = Math.Min(deckPartners, archetype.MaxDeckPartners) * archetype.PerDeckPartner;
            int relicBonus = relicPartners * archetype.PerRelicPartner;
            int value = (int)Math.Round(
                (archetype.BaseValue + deckBonus + relicBonus) * planMul * maturity * earlyMul * exerciseMul);

            total += value;
        }

        return Math.Clamp(total, -15, 55);
    }

    public static bool MatchesOffered(JsonObject card, CardMechanicProfile profile, ComboPartnerSpec spec) =>
        MatchesCard(card, profile, spec);

    public static bool MatchesCardPublic(JsonObject card, CardMechanicProfile profile, ComboPartnerSpec spec) =>
        MatchesCard(card, profile, spec);

    static CardMechanicProfile ResolveProfile(JsonObject card) {
        var id = card["id"]?.GetValue<string>();
        if (CardMechanicIndex.TryGet(id, out var profile))
            return profile;
        return CardMechanicIndex.InferFromSnapshot(card);
    }

    static int CountDeckPartners(JsonArray? deck, ComboPartnerSpec[] specs) {
        if (deck == null || specs.Length == 0)
            return 0;

        int count = 0;
        foreach (var node in deck) {
            if (node is not JsonObject card)
                continue;
            var profile = ResolveProfile(card);
            if (MatchesAnyCard(card, profile, specs))
                count++;
        }

        return count;
    }

    static int CountRelicPartners(JsonArray? relics, ComboPartnerSpec[] specs) {
        if (relics == null || specs.Length == 0)
            return 0;

        int count = 0;
        foreach (var node in relics) {
            string? id = node switch {
                JsonObject o => o["id"]?.GetValue<string>() ?? o["name"]?.GetValue<string>(),
                _ => node?.GetValue<string>(),
            };
            if (string.IsNullOrWhiteSpace(id))
                continue;
            if (MatchesAnyRelic(id, specs))
                count++;
        }

        return count;
    }

    static bool MatchesAnyCard(JsonObject card, CardMechanicProfile profile, ComboPartnerSpec[] specs) {
        foreach (var spec in specs) {
            if (MatchesCard(card, profile, spec))
                return true;
        }

        return false;
    }

    static bool MatchesAnyRelic(string relicId, ComboPartnerSpec[] specs) {
        foreach (var spec in specs) {
            if (MatchesRelic(relicId, spec))
                return true;
        }

        return false;
    }

    static bool MatchesCard(JsonObject card, CardMechanicProfile profile, ComboPartnerSpec spec) {
        var id = card["id"]?.GetValue<string>() ?? "";
        switch (spec.Kind) {
            case ComboPartnerKind.MechanicFlag:
                return TryParseFlag(spec.Token, out var flag) && profile.Flags.HasFlag(flag);
            case ComboPartnerKind.AiTag:
                return TryParseTag(spec.Token, out var tag)
                    && CardCatalog.ResolveTags(
                        id,
                        card["cardType"]?.GetValue<string>(),
                        card["keywords"]?.AsArray()).Contains(tag);
            case ComboPartnerKind.CardId:
                return string.Equals(id, spec.Token, StringComparison.OrdinalIgnoreCase);
            case ComboPartnerKind.IdContains:
                return id.Contains(spec.Token, StringComparison.OrdinalIgnoreCase);
            case ComboPartnerKind.InstallsPower:
                return TryParsePower(spec.Token, out var power) && profile.Installs(power);
            case ComboPartnerKind.HitScalePlayerBlock:
                return profile.HitScaleMode == AttackHitScaleMode.PlayerBlock;
            default:
                return false;
        }
    }

    static bool MatchesRelic(string relicId, ComboPartnerSpec spec) {
        switch (spec.Kind) {
            case ComboPartnerKind.RelicId:
                return string.Equals(relicId, spec.Token, StringComparison.OrdinalIgnoreCase);
            case ComboPartnerKind.RelicTag:
                return TryParseTag(spec.Token, out var tag)
                    && RelicCatalog.ResolveTags(relicId).Contains(tag);
            default:
                return false;
        }
    }

    static float PlanMultiplier(DeckPlan plan, AiTag[] tags) {
        if (tags.Length == 0)
            return 1f;

        float sum = 0f;
        foreach (var tag in tags)
            sum += plan.GetWeight(tag);

        return Math.Clamp(0.35f + sum * 0.22f, 0.3f, 1.65f);
    }

    static float MaturityMultiplier(int deckPartners, int relicPartners, int maxDeck, ComboRole role) {
        int partners = deckPartners + relicPartners;
        if (partners <= 0)
            return role switch {
                ComboRole.Enabler or ComboRole.Engine => 0.28f,
                ComboRole.Consumer or ComboRole.Terminal => 0.18f,
                ComboRole.Multiplier => 0.22f,
                _ => 0.2f,
            };

        float deckRatio = maxDeck > 0 ? Math.Min(1f, (float)deckPartners / maxDeck) : 0f;
        float relicBoost = Math.Min(0.35f, relicPartners * 0.12f);
        return Math.Clamp(0.42f + deckRatio * 0.48f + relicBoost, 0.42f, 1f);
    }

    static float EarlyActMultiplier(ComboRole role, int actIndex, int partners) {
        if (actIndex > 1 || partners > 0)
            return 1f;

        return role switch {
            ComboRole.Enabler or ComboRole.Engine => 1.12f,
            ComboRole.Terminal => 0.85f,
            _ => 1f,
        };
    }

    static bool TryParseFlag(string token, out CardMechanicFlags flag) =>
        Enum.TryParse(token, out flag);

    static bool TryParseTag(string token, out AiTag tag) =>
        Enum.TryParse(token, out tag);

    static bool TryParsePower(string token, out PlayerPowerEffectKind power) =>
        Enum.TryParse(token, out power);

    public sealed record ComboArchetype(
        string Id,
        ComboRole Role,
        ComboPartnerSpec Offered,
        ComboPartnerSpec[] DeckPartners,
        ComboPartnerSpec[] RelicPartners,
        AiTag[] PlanTags,
        int BaseValue,
        int PerDeckPartner,
        int PerRelicPartner,
        int MaxDeckPartners);
}
