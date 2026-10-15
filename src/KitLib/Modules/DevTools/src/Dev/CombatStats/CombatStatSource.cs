using KitLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.CombatStats;

internal enum CombatStatSourceKind {
    Unknown,
    Card,
    Power,
    Relic,
    Potion,
    Enemy,
    MonsterMove,
    Player,
    Synergy,
}

internal readonly struct CombatStatSource {
    public CombatStatSourceKind Kind { get; init; }
    public string Key { get; init; }
    public string Name { get; init; }

    public bool IsKnown => Kind != CombatStatSourceKind.Unknown && !string.IsNullOrWhiteSpace(Name);

    public static CombatStatSource Unknown => new() {
        Kind = CombatStatSourceKind.Unknown,
        Key = "",
        Name = "",
    };

    public static CombatStatSource FromCard(CardModel card) {
        string key = SafeEntry(card);
        string name = CombatStatsDisplayNames.ResolveCardName(card);
        return new CombatStatSource {
            Kind = CombatStatSourceKind.Card,
            Key = key,
            Name = string.IsNullOrWhiteSpace(name) ? key : name,
        };
    }

    public static CombatStatSource FromPower(PowerModel power) {
        string key = SafeEntry(power);
        return new CombatStatSource {
            Kind = CombatStatSourceKind.Power,
            Key = key,
            Name = CombatStatsDisplayNames.ResolvePowerName(power),
        };
    }

    public static CombatStatSource FromPower(string key, string displayName) => new() {
        Kind = CombatStatSourceKind.Power,
        Key = key,
        Name = displayName,
    };

    public static CombatStatSource FromRelic(RelicModel relic) {
        string key = SafeEntry(relic);
        return new CombatStatSource {
            Kind = CombatStatSourceKind.Relic,
            Key = key,
            Name = CombatStatsDisplayNames.ResolveRelicName(relic),
        };
    }

    public static CombatStatSource FromPotion(PotionModel potion) {
        string key = SafeEntry(potion);
        return new CombatStatSource {
            Kind = CombatStatSourceKind.Potion,
            Key = key,
            Name = CombatStatsDisplayNames.ResolvePotionName(potion),
        };
    }

    public static CombatStatSource FromMonster(MonsterModel monster) {
        string key = SafeEntry(monster);
        string name = key;
        try {
            string? title = monster.Title?.GetFormattedText();
            if (!string.IsNullOrWhiteSpace(title))
                name = title;
        }
        catch {
            // keep entry id
        }

        return new CombatStatSource {
            Kind = CombatStatSourceKind.Enemy,
            Key = key,
            Name = name,
        };
    }

    public static CombatStatSource FromCreature(Creature? creature) {
        if (creature == null)
            return Unknown;

        if (creature.IsMonster && creature.Monster != null)
            return FromMonster(creature.Monster);

        if (creature.IsPlayer) {
            string name = creature.Name;
            if (string.IsNullOrWhiteSpace(name))
                name = creature.Player?.NetId.ToString() ?? "player";
            return new CombatStatSource {
                Kind = CombatStatSourceKind.Player,
                Key = creature.Player?.NetId.ToString() ?? "",
                Name = name,
            };
        }

        return Unknown;
    }

    public static CombatStatSource MonsterMove() => new() {
        Kind = CombatStatSourceKind.MonsterMove,
        Key = "monster_move",
        Name = I18N.T("combatStats.source.monsterMove", "Monster move"),
    };

    public static CombatStatSource Synergy(string key, string displayName) => new() {
        Kind = CombatStatSourceKind.Synergy,
        Key = key,
        Name = displayName,
    };

    private static string SafeEntry(AbstractModel model) {
        try {
            return model.Id.Entry ?? "";
        }
        catch {
            return "";
        }
    }
}
