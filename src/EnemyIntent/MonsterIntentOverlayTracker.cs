using System;
using MegaCrit.Sts2.Core.Combat;

namespace KitLib.EnemyIntent;

/// <summary>Notifies UI when combat enemy intents may have changed.</summary>
internal static class MonsterIntentOverlayTracker {
    private static bool _initialized;

    public static event Action? Changed;

    public static void Initialize() {
        if (_initialized)
            return;
        _initialized = true;

        CombatManager.Instance.CombatSetUp += _ => NotifyChanged();
        CombatManager.Instance.CombatEnded += _ => NotifyChanged();
        CombatManager.Instance.TurnStarted += _ => NotifyChanged();
        CombatManager.Instance.TurnEnded += _ => NotifyChanged();
        CombatManager.Instance.CreaturesChanged += _ => NotifyChanged();
    }

    internal static void NotifyChanged() => Changed?.Invoke();
}
