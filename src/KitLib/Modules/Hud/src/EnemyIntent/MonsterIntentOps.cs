using System;
using System.Collections.Generic;
using KitLib.Host;
using MegaCrit.Sts2.Core.Combat;

namespace KitLib.EnemyIntent;

internal static class MonsterIntentOps {
    internal static bool IsOverlayCombatReady(CombatState? state) =>
        KitLibHost.IsMonsterIntentOverlayReady?.Invoke(state) ?? false;

    internal static IReadOnlyList<MonsterIntentEntry> CaptureCurrent(CombatState? state) =>
        (IReadOnlyList<MonsterIntentEntry>?)KitLibHost.CaptureMonsterIntentCurrent?.Invoke(state)
        ?? Array.Empty<MonsterIntentEntry>();

    internal static IReadOnlyList<MonsterIntentEntry> CaptureNextTurn(CombatState? state) =>
        (IReadOnlyList<MonsterIntentEntry>?)KitLibHost.CaptureMonsterIntentNextTurn?.Invoke(state)
        ?? Array.Empty<MonsterIntentEntry>();
}
