using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.Actions;
using KitLib.AI.Combat.Simulation;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.AI.Knowledge;

public sealed record CardMechanicProfile(
    string Id,
    CardMechanicFlags Flags,
    IReadOnlyList<string> DynamicVarKeys,
    int CanonicalCost,
    int? Damage,
    int? Block,
    string CardType,
    IReadOnlyList<AiTag> DerivedTags,
    int AppliedVulnerable = 0,
    int AppliedWeak = 0,
    int AttackHitCount = 1,
    bool CostsEnergyX = false,
    bool AttackHitsScaleWithEnergy = false,
    int HpLoss = 0,
    int ReplayCount = 0,
    AttackHitScaleMode HitScaleMode = AttackHitScaleMode.None,
    IReadOnlyList<PlayerPowerInstall> PowerInstalls = null!) {
    public IReadOnlyList<PlayerPowerInstall> PowerInstalls { get; init; } =
        PowerInstalls ?? Array.Empty<PlayerPowerInstall>();

    public bool Installs(PlayerPowerEffectKind kind) {
        foreach (var install in PowerInstalls) {
            if (install.Kind == kind)
                return true;
        }

        return false;
    }

    public int InstallAmount(PlayerPowerEffectKind kind) {
        int total = 0;
        foreach (var install in PowerInstalls) {
            if (install.Kind == kind)
                total += install.Amount;
        }

        return total;
    }
}

/// <summary>Indexes official card mechanics from <see cref="ModelDb.AllCards"/> at startup.</summary>
public static class CardMechanicIndex {
    static readonly Dictionary<string, CardMechanicProfile> ById = new(StringComparer.OrdinalIgnoreCase);
    static bool _initialized;

    public static void Initialize() {
        if (_initialized) return;
        _initialized = true;

        foreach (var card in ModelDb.AllCards) {
            try {
                var id = card.Id.Entry ?? "";
                if (string.IsNullOrWhiteSpace(id)) continue;
                ById[id] = BuildProfile(card);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[AiMechanic] Skipped card {card.Id.Entry}: {ex.Message}");
            }
        }

        MainFile.Logger.Info($"[AiMechanic] CardMechanicIndex indexed {ById.Count} cards.");
    }

    public static bool TryGet(string? id, out CardMechanicProfile profile) {
        EnsureInitialized();
        profile = null!;
        if (string.IsNullOrWhiteSpace(id)) return false;
        return ById.TryGetValue(id, out profile!);
    }

    public static CardMechanicProfile InferFromSnapshot(JsonObject card) {
        var id = card["id"]?.GetValue<string>();
        if (TryGet(id, out var profile))
            return profile;

        var flags = CardMechanicFlags.None;
        var cardType = card["cardType"]?.GetValue<string>() ?? "";
        if (cardType.Contains("Attack", StringComparison.OrdinalIgnoreCase))
            flags |= CardMechanicFlags.HasDamage;
        if (card["damage"] != null)
            flags |= CardMechanicFlags.HasDamage;
        if (card["block"] != null)
            flags |= CardMechanicFlags.HasBlock;

        var keywords = card["keywords"]?.AsArray();
        if (keywords != null) {
            foreach (var node in keywords) {
                var kw = node?.GetValue<string>() ?? "";
                flags |= OfficialMechanicProbe.FlagsFromKeywordName(kw);
            }
        }

        flags |= OfficialMechanicProbe.AnalyzeTokenBlob(id ?? "");

        var cost = card["cost"]?.GetValue<int>() ?? 1;
        var costsEnergyX = card["costsX"]?.GetValue<bool>() == true;
        var derived = CardTagRules.InferTagsFromSnapshot(id, cardType, keywords);
        return new CardMechanicProfile(
            id ?? "",
            flags,
            [],
            cost,
            card["damage"]?.GetValue<int>(),
            card["block"]?.GetValue<int>(),
            cardType,
            derived,
            CostsEnergyX: costsEnergyX);
    }

    static CardMechanicProfile BuildProfile(CardModel card) {
        var id = card.Id.Entry ?? "";
        var flags = OfficialMechanicProbe.ProbeCard(card);

        if (OfficialMechanicProbe.NeedsCardTextFallback(flags)) {
            try {
                flags |= MechanicTextAnalyzer.AnalyzeCardTextFallback(
                    CardPreviewHelper.GetDescription(card),
                    card.GetType().Name);
            }
            catch { /* ignore */ }
        }

        var derived = new HashSet<AiTag>(CardTagRules.InferTags(card));
        derived.UnionWith(TagsFromFlags(flags));

        var (appliedVuln, appliedWeak) = ReadAppliedEnemyPowers(card);
        if (appliedVuln > 0) flags |= CardMechanicFlags.AppliesVulnerable;
        if (appliedWeak > 0) flags |= CardMechanicFlags.AppliesWeak;

        if (string.Equals(card.GetType().Name, "Havoc", StringComparison.Ordinal))
            flags |= CardMechanicFlags.PlaysTopOfDrawExhaust;

        var costsEnergyX = card.EnergyCost.CostsX;
        var hitScale = ReadHitScaleMode(card, costsEnergyX);
        var powerInstalls = PlayerPowerEffectIndex.ReadInstalls(card);
        return new CardMechanicProfile(
            id,
            flags,
            CardEditActions.GetDynamicVarKeys(card).ToArray(),
            card.EnergyCost.Canonical,
            CardEditActions.GetDamage(card),
            CardEditActions.GetBlock(card),
            card.Type.ToString(),
            [.. derived],
            appliedVuln,
            appliedWeak,
            ReadAttackHitCount(card, hitScale),
            costsEnergyX,
            hitScale == AttackHitScaleMode.Energy,
            ReadHpLoss(card),
            CardEditActions.GetReplayCount(card) ?? 0,
            hitScale,
            powerInstalls);
    }

    static int ReadAttackHitCount(CardModel card, AttackHitScaleMode hitScale) {
        if (hitScale != AttackHitScaleMode.None && hitScale != AttackHitScaleMode.UnblockedDamageTakenPlusOne)
            return 1;

        var repeat = CardEditActions.GetDynamicVar(card, "Repeat");
        if (repeat is > 0)
            return repeat.Value;

        return AttackHitCountByTypeName.TryGetValue(card.GetType().Name, out var hits) ? hits : 1;
    }

    static AttackHitScaleMode ReadHitScaleMode(CardModel card, bool costsEnergyX) {
        if (costsEnergyX && card.Type == CardType.Attack
            && AttackHitsScaleWithEnergyByTypeName.Contains(card.GetType().Name))
            return AttackHitScaleMode.Energy;

        return HitScaleModeByTypeName.TryGetValue(card.GetType().Name, out var mode)
            ? mode
            : AttackHitScaleMode.None;
    }

    static int ReadHpLoss(CardModel card) {
        var hpLoss = CardEditActions.GetDynamicVar(card, "HpLoss");
        return hpLoss is > 0 ? hpLoss.Value : 0;
    }

    static readonly Dictionary<string, AttackHitScaleMode> HitScaleModeByTypeName =
        new(StringComparer.Ordinal) {
            ["Finisher"] = AttackHitScaleMode.AttacksPlayedThisTurn,
            ["Flechettes"] = AttackHitScaleMode.SkillsInHand,
            ["Barrage"] = AttackHitScaleMode.OrbCount,
            ["FlakCannon"] = AttackHitScaleMode.StatusCardsOwned,
            ["TearAsunder"] = AttackHitScaleMode.UnblockedDamageTakenPlusOne,
        };

    static readonly HashSet<string> AttackHitsScaleWithEnergyByTypeName =
        new(StringComparer.Ordinal) {
            "Skewer",
            "Eradicate",
            "Volley",
            "Whirlwind",
            "HeavenlyDrill",
        };

    static readonly Dictionary<string, int> AttackHitCountByTypeName =
        new(StringComparer.Ordinal) {
            ["TwinStrike"] = 2,
            ["Uproar"] = 2,
            ["RipAndTear"] = 2,
            ["DaggerSpray"] = 2,
            ["Refract"] = 2,
            ["Maul"] = 2,
        };

    static (int Vulnerable, int Weak) ReadAppliedEnemyPowers(CardModel card) {
        int vuln = 0, weak = 0;
        foreach (var key in CardEditActions.GetDynamicVarKeys(card)) {
            var amount = CardEditActions.GetDynamicVar(card, key) ?? 0;
            if (amount <= 0) continue;
            var upper = key.ToUpperInvariant();
            if (upper.Contains("VULNERABLE", StringComparison.Ordinal))
                vuln = Math.Max(vuln, amount);
            if (upper.Contains("WEAK", StringComparison.Ordinal))
                weak = Math.Max(weak, amount);
        }
        return (vuln, weak);
    }

    static IEnumerable<AiTag> TagsFromFlags(CardMechanicFlags flags) {
        if (flags.HasFlag(CardMechanicFlags.HasDraw)) yield return AiTag.Draw;
        if (flags.HasFlag(CardMechanicFlags.HasForge)) yield return AiTag.Scaling;
        if (flags.HasFlag(CardMechanicFlags.HasStarCost)) yield return AiTag.Energy;
        if (flags.HasFlag(CardMechanicFlags.Exhaust)) yield return AiTag.Exhaust;
        if (flags.HasFlag(CardMechanicFlags.Retain)) yield return AiTag.Setup;
        if (flags.HasFlag(CardMechanicFlags.Aoe)) yield return AiTag.Aoe;
        if (flags.HasFlag(CardMechanicFlags.TransformsHandAttacks)
            || flags.HasFlag(CardMechanicFlags.TransformsCards))
            yield return AiTag.Scaling;
        if (flags.HasFlag(CardMechanicFlags.HasDamage)) yield return AiTag.Attack;
        if (flags.HasFlag(CardMechanicFlags.HasBlock)) yield return AiTag.Block;
        if (flags.HasFlag(CardMechanicFlags.AppliesVulnerable)
            || flags.HasFlag(CardMechanicFlags.AppliesWeak))
            yield return AiTag.Setup;
    }

    static void EnsureInitialized() {
        if (!_initialized)
            Initialize();
    }

    internal static void ClearForTests() {
        ById.Clear();
        _initialized = false;
    }
}
