using System;
using KitLib.Combat;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.Patches;

/// <summary>Auto-saves combat checkpoint nodes; independent of hook trigger IsActive gate.</summary>
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetUpCombat))]
internal static class CombatCheckpointPatch {
    private static Action<CombatState>? _turnStartHandler;
    private static Action<CombatRoom>? _combatEndHandler;

    [HarmonyPostfix]
    private static void Postfix(CombatManager __instance) {
        if (_turnStartHandler != null)
            __instance.TurnStarted -= _turnStartHandler;
        if (_combatEndHandler != null)
            __instance.CombatEnded -= _combatEndHandler;

        CombatCheckpointStore.BeginCombat();

        _turnStartHandler = combatState => {
            if (combatState.CurrentSide != CombatSide.Player)
                return;
            // Double-defer so opening draw / turn setup finishes before the snapshot.
            Callable.From(() => Callable.From(OnPlayerTurnStarted).CallDeferred()).CallDeferred();
        };

        _combatEndHandler = _ => CombatCheckpointStore.EndCombat();

        __instance.TurnStarted += _turnStartHandler;
        __instance.CombatEnded += _combatEndHandler;
    }

    private static void OnPlayerTurnStarted() {
        if (!KitLibState.IsActive)
            return;
        if (CombatManager.Instance is not { IsInProgress: true })
            return;
        if (CombatManager.Instance.DebugOnlyGetState() is not { CurrentSide: CombatSide.Player })
            return;
        if (!RunContext.TryGetRunAndPlayer(out _, out _))
            return;

        int round = CombatManager.Instance.DebugOnlyGetState()!.RoundNumber;
        CombatCheckpointStore.SaveNode(CombatCheckpointKind.CombatStart, round);
        CombatCheckpointStore.SaveNode(CombatCheckpointKind.TurnStart, round);
    }
}
