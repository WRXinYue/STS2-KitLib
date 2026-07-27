using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace KitLib.AI.Tests.Combat;

/// <summary>
/// Boots vanilla <see cref="ModelDb"/> for headless stat checks (no CardMechanicIndex / Godot assets).
/// </summary>
internal static class OfficialCombatSimBootstrap {
    static bool _attempted;
    static bool _ready;
    static string? _error;
    static readonly object Gate = new();

    public static bool IsReady => _ready;

    public static string? BootstrapError => _error;

    public static void EnsureReady() {
        if (_ready)
            return;

        lock (Gate) {
            if (_ready || _attempted)
                return;

            _attempted = true;
            try {
                ModelDb.Init(AbstractModelSubtypes.All.ToArray());
                _ready = true;
            }
            catch (Exception ex) {
                _error = ex.ToString();
            }
        }
    }

    public static void VerifyStarterStatsMatchSandbox() {
        try {
            EnsureReady();
            if (!_ready)
                throw new InvalidOperationException(_error ?? "ModelDb bootstrap failed.");

            AssertStat(OfficialIroncladCards.Bash, ModelDb.Card<Bash>(), 2, 8, null, 2);
            AssertStat(OfficialIroncladCards.Strike, ModelDb.Card<StrikeIronclad>(), 1, 6, null, null);
            AssertStat(OfficialIroncladCards.Defend, ModelDb.Card<DefendIronclad>(), 1, null, 5, null);
        }
        finally {
            if (_ready) {
                ModelDb.ResetForTest();
                _ready = false;
                _attempted = false;
            }
        }
    }

    static void AssertStat(
        string id,
        CardModel card,
        int cost,
        int? damage,
        int? block,
        int? vulnerable) {
        if (card.EnergyCost.Canonical != cost)
            throw new InvalidOperationException($"{id}: cost {card.EnergyCost.Canonical} != {cost}");

        if (damage.HasValue && ReadDynamicInt(card, "Damage") != damage)
            throw new InvalidOperationException($"{id}: damage {ReadDynamicInt(card, "Damage")} != {damage}");

        if (block.HasValue && ReadDynamicInt(card, "Block") != block)
            throw new InvalidOperationException($"{id}: block {ReadDynamicInt(card, "Block")} != {block}");

        if (vulnerable.HasValue && ReadDynamicInt(card, "VulnerablePower") != vulnerable)
            throw new InvalidOperationException($"{id}: vulnerable {ReadDynamicInt(card, "VulnerablePower")} != {vulnerable}");
    }

    static int ReadDynamicInt(CardModel card, string key) {
        if (!card.DynamicVars.TryGetValue(key, out var dv))
            return 0;
        return (int)dv.BaseValue;
    }
}

internal static class OfficialIroncladCards {
    public const string Strike = "STRIKE_IRONCLAD";
    public const string Defend = "DEFEND_IRONCLAD";
    public const string Bash = "BASH";
}
