using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat.Simulation;

public sealed record CombatHandCard(
    int HandIndex,
    string Id,
    string Name,
    int Cost,
    int Damage,
    int Block,
    string CardType,
    string TargetType,
    bool CanPlay,
    CardMechanicProfile Profile,
    bool IsAoe) {
    public bool IsAttack => CombatCardStats.IsAttackCard(ToJson());
    public bool IsSkill => CombatCardStats.IsSkillCard(ToJson());

    public System.Text.Json.Nodes.JsonObject ToJson() => new() {
        ["id"] = Id,
        ["name"] = Name,
        ["cost"] = Cost,
        ["damage"] = Damage > 0 ? Damage : null,
        ["block"] = Block > 0 ? Block : null,
        ["cardType"] = CardType,
        ["targetType"] = TargetType,
        ["canPlay"] = CanPlay,
    };
}
