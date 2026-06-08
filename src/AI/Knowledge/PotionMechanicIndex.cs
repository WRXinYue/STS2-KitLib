using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.AI.Knowledge;

/// <summary>Indexes official potion mechanics from <see cref="ModelDb.AllPotions"/> at startup.</summary>
public static class PotionMechanicIndex {
    static readonly Dictionary<string, PotionMechanicProfile> ById = new(StringComparer.OrdinalIgnoreCase);
    static bool _initialized;

    public static void Initialize() {
        if (_initialized) return;
        _initialized = true;

        foreach (var potion in ModelDb.AllPotions) {
            try {
                var id = potion.Id.Entry ?? "";
                if (string.IsNullOrWhiteSpace(id)) continue;
                ById[id] = BuildProfile(potion);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[AiMechanic] Skipped potion {potion.Id.Entry}: {ex.Message}");
            }
        }

        MainFile.Logger.Info($"[AiMechanic] PotionMechanicIndex indexed {ById.Count} potions.");
    }

    public static bool TryGet(string? id, out PotionMechanicProfile profile) {
        EnsureInitialized();
        profile = null!;
        if (string.IsNullOrWhiteSpace(id)) return false;
        return ById.TryGetValue(id, out profile!);
    }

    public static PotionMechanicProfile GetOrDefault(string? id) {
        if (TryGet(id, out var profile)) return profile;
        var normalized = NormalizeId(id);
        return new PotionMechanicProfile(
            normalized,
            ClassifyFromId(normalized),
            PotionUsage.CombatOnly.ToString(),
            "",
            "",
            0,
            0);
    }

    static PotionMechanicProfile BuildProfile(PotionModel potion) {
        var id = potion.Id.Entry ?? "";
        var typeName = potion.GetType().Name;
        var category = ClassifyFromTypeName(typeName);
        if (category == PotionCategory.Unknown)
            category = ClassifyFromId(id);

        if (category == PotionCategory.Unknown) {
            try {
                category = ClassifyFromDescription(potion.DynamicDescription?.GetFormattedText() ?? "");
            }
            catch { /* ignore */ }
        }

        return new PotionMechanicProfile(
            id,
            category,
            potion.Usage.ToString(),
            potion.TargetType.ToString(),
            potion.Rarity.ToString(),
            EstimateBlock(typeName, id),
            EstimateDamage(typeName, id));
    }

    static PotionCategory ClassifyFromTypeName(string typeName) {
        var upper = typeName.ToUpperInvariant();
        if (upper.Contains("BLOCK") || upper is "FORTIFIER" or "LIQUIDBRONZE" or "GHOSTINAJAR")
            return PotionCategory.Block;
        if (upper.Contains("BLOOD") || upper.Contains("FRUIT") || upper.Contains("REGEN")
            || upper is "FAIRYINABOTTLE" or "CUREALL")
            return PotionCategory.Heal;
        if (upper is "EXPLOSIVEAMPOULE" or "POTOFGHOULS" or "POTIONOFDOOM")
            return upper.Contains("DOOM") ? PotionCategory.DamageSingle : PotionCategory.DamageAoE;
        if (upper is "FIREPOTION" or "POTIONSHAPEDROCK" or "POWDEREDDEMISE" or "GIGANTIFICATIONPOTION")
            return PotionCategory.DamageSingle;
        if (upper is "ENERGYPOTION" or "STARPOTION")
            return PotionCategory.Energy;
        if (upper is "SWIFTPOTION" or "BOTTLEDPOTENTIAL" or "LIQUIDMEMORIES" or "CLARITY")
            return PotionCategory.Draw;
        if (upper.Contains("STRENGTH") || upper.Contains("DEXTERITY") || upper is "FLEXPOTION"
            || upper is "POWERPOTION" or "FOCUSPOTION" or "SOLDIERSSTEW" or "BLESSINGOFTHEFORGE")
            return PotionCategory.Buff;
        if (upper is "SKILLPOTION" or "ATTACKPOTION" or "COLORLESSPOTION" or "CUNNINGPOTION"
            || upper is "GAMBLERSBREW" or "ENTROPICBREW" or "COSMICCONCOCTION")
            return PotionCategory.Random;
        if (upper.Contains("WEAK") || upper.Contains("VULNERABLE") || upper is "POISONPOTION"
            || upper is "SHACKLINGPOTION" or "POTIONOFBINDING")
            return PotionCategory.Debuff;
        if (upper is "SPEEDPOTION")
            return PotionCategory.Buff;
        if (upper is "FOULPOTION" or "SNECKOIL" or "DUPLICATOR"
            || upper is "STABLESERUM" or "LUCKYTONIC")
            return PotionCategory.Utility;
        return PotionCategory.Unknown;
    }

    static PotionCategory ClassifyFromId(string id) {
        var upper = id.ToUpperInvariant();
        if (upper.Contains("BLOCK") || upper.Contains("BRONZE") || upper.Contains("FORTIF"))
            return PotionCategory.Block;
        if (upper.Contains("BLOOD") || upper.Contains("FRUIT") || upper.Contains("REGEN")
            || upper.Contains("FAIRY") || upper.Contains("CURE"))
            return PotionCategory.Heal;
        if (upper.Contains("EXPLOSIVE") || upper.Contains("GHOUL"))
            return PotionCategory.DamageAoE;
        if (upper.Contains("FIRE") || upper.Contains("SHAPED_ROCK") || upper.Contains("DOOM")
            || upper.Contains("DEMISE") || upper.Contains("GIGANT"))
            return PotionCategory.DamageSingle;
        if (upper.Contains("ENERGY") || upper.Contains("STAR_POTION"))
            return PotionCategory.Energy;
        if (upper.Contains("SWIFT") || upper.Contains("POTENTIAL") || upper.Contains("CLARITY")
            || upper.Contains("MEMORIES"))
            return PotionCategory.Draw;
        if (upper.Contains("STRENGTH") || upper.Contains("DEXTERITY") || upper.Contains("FLEX")
            || upper.Contains("SPEED") || upper.Contains("POWER") || upper.Contains("FOCUS")
            || upper.Contains("STEW"))
            return PotionCategory.Buff;
        if (upper.Contains("SKILL") || upper.Contains("ATTACK") || upper.Contains("COLORLESS")
            || upper.Contains("CUNNING") || upper.Contains("GAMBLER") || upper.Contains("ENTROPIC"))
            return PotionCategory.Random;
        if (upper.Contains("WEAK") || upper.Contains("VULNERABLE") || upper.Contains("POISON")
            || upper.Contains("SHACKL") || upper.Contains("BINDING"))
            return PotionCategory.Debuff;
        return PotionCategory.Unknown;
    }

    static PotionCategory ClassifyFromDescription(string text) {
        var upper = text.ToUpperInvariant();
        if (upper.Contains("BLOCK")) return PotionCategory.Block;
        if (upper.Contains("HEAL") || upper.Contains("HP")) return PotionCategory.Heal;
        if (upper.Contains("ENERGY")) return PotionCategory.Energy;
        if (upper.Contains("DRAW")) return PotionCategory.Draw;
        if (upper.Contains("STRENGTH") || upper.Contains("DEXTERITY")) return PotionCategory.Buff;
        if (upper.Contains("DAMAGE") || upper.Contains("ATTACK")) return PotionCategory.DamageSingle;
        if (upper.Contains("WEAK") || upper.Contains("VULNERABLE") || upper.Contains("POISON"))
            return PotionCategory.Debuff;
        return PotionCategory.Unknown;
    }

    static int EstimateBlock(string typeName, string id) {
        var upper = (typeName + id).ToUpperInvariant();
        if (upper.Contains("BLOCK")) return 12;
        if (upper.Contains("FORTIF") || upper.Contains("BRONZE")) return 10;
        return 0;
    }

    static int EstimateDamage(string typeName, string id) {
        var upper = (typeName + id).ToUpperInvariant();
        if (upper.Contains("SHAPED_ROCK") || upper.Contains("POTIONSHAPEDROCK")) return 20;
        if (upper.Contains("FIRE")) return 20;
        if (upper.Contains("EXPLOSIVE")) return 10;
        return 0;
    }

    static string NormalizeId(string? id) {
        if (string.IsNullOrWhiteSpace(id)) return "";
        var s = id.Trim();
        if (s.StartsWith("POTION.", StringComparison.OrdinalIgnoreCase))
            s = s["POTION.".Length..];
        return s.ToUpperInvariant();
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
