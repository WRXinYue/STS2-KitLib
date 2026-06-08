using System;
using KitLib.Hooks;
using KitLib.Scripts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace KitLib.Patches;

/// <summary>
/// Subscribe to CombatManager events on combat setup to fire Hook triggers
/// for CombatStart, CombatEnd, TurnStart, and TurnEnd.
/// </summary>
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetUpCombat))]
public static class HookCombatSetupPatch {
    private static Action<CombatState>? _turnStartHandler;
    private static Action<CombatState>? _turnEndHandler;
    private static Action<CombatRoom>? _combatEndHandler;

    public static void Postfix(CombatManager __instance) {
        if (!KitLibState.IsActive) return;

        // Unsubscribe stale handlers from a previous combat session
        if (_turnStartHandler != null) __instance.TurnStarted -= _turnStartHandler;
        if (_turnEndHandler != null) __instance.TurnEnded -= _turnEndHandler;
        if (_combatEndHandler != null) __instance.CombatEnded -= _combatEndHandler;

        bool firstTurn = true;

        _turnStartHandler = combatState => {
            ScriptManager.ProcessPendingReload();
            RunContext.TryGetRunAndPlayer(out var runState, out var p);
            if (firstTurn) {
                firstTurn = false;
                HookManager.Fire(TriggerType.CombatStart, p);
                ScriptManager.Fire(TriggerType.CombatStart, p);
            }
            HookManager.Fire(TriggerType.TurnStart, p);
            ScriptManager.Fire(TriggerType.TurnStart, p);
        };

        _turnEndHandler = combatState => {
            RunContext.TryGetRunAndPlayer(out var runState, out var p);
            HookManager.Fire(TriggerType.TurnEnd, p);
            ScriptManager.Fire(TriggerType.TurnEnd, p);
        };

        _combatEndHandler = room => {
            RunContext.TryGetRunAndPlayer(out var runState, out var p);
            HookManager.Fire(TriggerType.CombatEnd, p);
            ScriptManager.Fire(TriggerType.CombatEnd, p);
        };

        __instance.TurnStarted += _turnStartHandler;
        __instance.TurnEnded += _turnEndHandler;
        __instance.CombatEnded += _combatEndHandler;
    }
}

/// <summary>Fire OnDraw trigger when cards are drawn.</summary>
[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Draw),
    [typeof(PlayerChoiceContext), typeof(decimal), typeof(Player), typeof(bool)])]
public static class HookDrawPatch {
    public static void Postfix(Player player) {
        if (!KitLibState.IsActive) return;
        HookManager.Fire(TriggerType.OnDraw, player);
        ScriptManager.Fire(TriggerType.OnDraw, player);
    }
}

/// <summary>Fire OnDamageTaken / OnDamageDealt when a creature loses HP.</summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal))]
[HarmonyPriority(Priority.Low)]
public static class HookDamagePatch {
    public static void Postfix(Creature __instance, DamageResult __result) {
        if (!KitLibState.IsActive) return;
        if (__result.UnblockedDamage <= 0) return;

        Player? player = null;
        RunContext.TryGetRunAndPlayer(out _, out player);

        if (__instance.Player != null) {
            HookManager.Fire(TriggerType.OnDamageTaken, player);
            ScriptManager.Fire(TriggerType.OnDamageTaken, player);
        }
        else {
            HookManager.Fire(TriggerType.OnDamageDealt, player);
            ScriptManager.Fire(TriggerType.OnDamageDealt, player);
        }
    }
}

/// <summary>Fire OnPotionUsed when a potion is consumed.</summary>
[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.OnUseWrapper))]
public static class HookPotionUsedPatch {
    public static void Prefix() {
        if (!KitLibState.IsActive) return;

        Player? player = null;
        RunContext.TryGetRunAndPlayer(out _, out player);
        HookManager.Fire(TriggerType.OnPotionUsed, player);
        ScriptManager.Fire(TriggerType.OnPotionUsed, player);
    }
}

/// <summary>
/// Fire OnCardPlayed — applied at runtime via <see cref="ScriptCardPlayedPatch.TryApply"/>
/// because the target method name varies across STS2 versions.
/// </summary>
public static class ScriptCardPlayedPatch {
    private static readonly string[] CandidateMethods = ["PlayCard", "UseCard", "Play"];

    public static void TryApply(HarmonyLib.Harmony harmony) {
        var cmType = typeof(CombatManager);
        foreach (var name in CandidateMethods) {
            var method = cmType.GetMethod(name,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (method == null) continue;

            try {
                var postfix = new HarmonyMethod(typeof(ScriptCardPlayedPatch), nameof(Postfix));
                harmony.Patch(method, postfix: postfix);
                MainFile.Logger.Info($"[HookPatches] OnCardPlayed bound to CombatManager.{name}");
                return;
            }
            catch (System.Exception ex) {
                MainFile.Logger.Warn($"[HookPatches] Failed to patch CombatManager.{name}: {ex.Message}");
            }
        }
        MainFile.Logger.Info("[HookPatches] OnCardPlayed: no matching method found — trigger disabled.");
    }

    public static void Postfix() {
        if (!KitLibState.IsActive) return;
        Player? player = null;
        RunContext.TryGetRunAndPlayer(out _, out player);
        HookManager.Fire(TriggerType.OnCardPlayed, player);
        ScriptManager.Fire(TriggerType.OnCardPlayed, player);
    }
}

/// <summary>Fire OnShuffle when the draw pile is shuffled.</summary>
[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Shuffle))]
public static class ScriptShufflePatch {
    public static void Postfix() {
        if (!KitLibState.IsActive) return;
        Player? player = null;
        RunContext.TryGetRunAndPlayer(out _, out player);
        HookManager.Fire(TriggerType.OnShuffle, player);
        ScriptManager.Fire(TriggerType.OnShuffle, player);
    }
}
