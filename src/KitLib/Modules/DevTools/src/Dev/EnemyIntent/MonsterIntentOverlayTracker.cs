using System;
using KitLib;
using MegaCrit.Sts2.Core.Combat;

namespace KitLib.EnemyIntent;

/// <summary>Notifies UI when combat enemy intents may have changed.</summary>
internal static class MonsterIntentOverlayTracker {
    private static bool _initialized;
    private static bool _wired;

    public static event Action? Changed;

    public static void Initialize() {
        if (_initialized)
            return;
        _initialized = true;
    }

    internal static void EnsureWired() {
        if (_wired) return;
        try {
            var combatManager = CombatManager.Instance;
            if (combatManager == null)
                return;

            combatManager.CombatSetUp += _ => NotifyChanged();
            combatManager.CombatEnded += _ => NotifyChanged();
            combatManager.TurnStarted += _ => NotifyChanged();
            combatManager.TurnEnded += _ => NotifyChanged();
            combatManager.CreaturesChanged += _ => NotifyChanged();
            _wired = true;
        }
        catch (Exception) {
        }
    }

    internal static void NotifyChanged() => Changed?.Invoke();
}
