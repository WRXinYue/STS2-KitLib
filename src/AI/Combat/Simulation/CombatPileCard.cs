using System;
using System.Text.Json.Nodes;
using KitLib.AI.Combat;

namespace KitLib.AI.Combat.Simulation;

public sealed record CombatPileCard(
    string Id,
    string Name,
    int Cost,
    int Damage,
    int Block,
    string CardType,
    bool IsStatus,
    bool HasRetain,
    bool HasExhaust) : IComparable<CombatPileCard> {
    public int CompareTo(CombatPileCard? other) {
        if (other is null) return 1;
        var idCmp = string.Compare(Id, other.Id, StringComparison.Ordinal);
        return idCmp != 0 ? idCmp : string.Compare(Name, other.Name, StringComparison.Ordinal);
    }
    public static CombatPileCard FromJson(JsonObject card) {
        var id = card["id"]?.GetValue<string>() ?? "";
        return new CombatPileCard(
            id,
            card["name"]?.GetValue<string>() ?? id,
            card["cost"]?.GetValue<int>() ?? 1,
            CombatCardStats.ResolveDamage(card),
            CombatCardStats.ResolveBlock(card),
            card["cardType"]?.GetValue<string>() ?? "",
            card["isStatus"]?.GetValue<bool>()
                ?? CombatJunkCard.IsJunkId(id, card["rarity"]?.GetValue<string>()),
            card["hasRetain"]?.GetValue<bool>() == true,
            card["hasExhaust"]?.GetValue<bool>() == true);
    }

    public JsonObject ToJson() => new() {
        ["id"] = Id,
        ["name"] = Name,
        ["cost"] = Cost,
        ["damage"] = Damage,
        ["block"] = Block,
        ["cardType"] = CardType,
        ["isStatus"] = IsStatus,
        ["hasRetain"] = HasRetain,
        ["hasExhaust"] = HasExhaust,
    };
}
