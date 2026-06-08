namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>Serializes LanLocal, Companion, and MpAi decision ticks on one shared executor.</summary>
internal static class AiDecisionGate {
    static bool _busy;

    public static bool TryEnter() {
        if (_busy) return false;
        _busy = true;
        return true;
    }

    public static void Exit() => _busy = false;

    public static void Reset() => _busy = false;

    public static bool IsCombatInProgress =>
        MegaCrit.Sts2.Core.Combat.CombatManager.Instance is { IsInProgress: true };
}
