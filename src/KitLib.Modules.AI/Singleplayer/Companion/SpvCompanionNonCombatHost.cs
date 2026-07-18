using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KitLib.AI;
using KitLib.AI.AutoPlay;
using KitLib.AI.Core;
using KitLib.AI.Core.Schema;
using KitLib.AI.Sts2.Helpers;
using KitLib.Companion;
using KitLib.Multiplayer.PseudoCoop;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Singleplayer.Companion;

/// <summary>Runs <see cref="GameLoop"/> for SP companions during non-combat phases.</summary>
internal static class SpvCompanionNonCombatHost {
    static bool _tickRunning;
    static readonly Dictionary<ulong, GameLoop> _loops = [];

    static readonly GamePhase[] NonCombatPhases = [
        GamePhase.MapSelection,
        GamePhase.CardReward,
        GamePhase.EventChoice,
        GamePhase.Shop,
        GamePhase.RestSite,
        GamePhase.RewardScreen,
        GamePhase.RelicSelection,
        GamePhase.PostCombatTransition,
        GamePhase.TreasureRoom,
    ];

    public static bool IsEnabled =>
        SpvCompanionRegistry.HasAny && SpvCompanionRegistry.IsSingleplayerRun();

    public static void OnRunEnded() {
        _tickRunning = false;
        _loops.Clear();
    }

    public static void Poll(double delta, ref double accum) {
        if (!IsEnabled) return;

        accum += delta;
        if (accum < 0.6) return;
        accum = 0;

        if (_tickRunning) return;
        if (AiDecisionGate.IsCombatInProgress) return;
        if (!AiDecisionGate.TryEnter()) return;

        var phase = AiPlayServices.StateProvider.CurrentPhase;
        if (phase is GamePhase.None or GamePhase.Combat or GamePhase.GameOver or GamePhase.Victory) {
            AiDecisionGate.Exit();
            return;
        }

        if (!NonCombatPhases.Contains(phase)) {
            AiDecisionGate.Exit();
            return;
        }

        if (RunManager.Instance?.DebugOnlyGetState() is not { } state) {
            AiDecisionGate.Exit();
            return;
        }

        var local = LocalContext.GetMe(state.Players);
        var anyStarted = false;

        foreach (var player in SpvCompanionRegistry.GetCombatTargets()) {
            if (!ShouldRunNonCombatFor(player)) continue;

            SpvCompanionMapVote.TryMirrorLocalVote(player);

            if (local != null && SpvCompanionLocalUiGuard.BlocksCompanionGameLoop(phase, state, local, player))
                continue;

            anyStarted = true;
            _tickRunning = true;
            TaskHelper.RunSafely(RunDecisionAsync(player, phase));
            return;
        }

        if (!anyStarted)
            AiDecisionGate.Exit();
    }

    static bool ShouldRunNonCombatFor(Player player) {
        if (CompanionNonCombatRegistry.IsEnabled(player.NetId))
            return true;

        var characterId = player.Character?.Id.Entry;
        return CharacterAiRegistry.SupportsNonCombat(characterId);
    }

    static async Task RunDecisionAsync(Player player, GamePhase phase) {
        try {
            AiHostContext.ActiveNetId = player.NetId;
            var loop = GetOrCreateLoop(player);
            await loop.OnDecisionPointAsync(phase);
        }
        catch (Exception ex) {
            KitLog.Warn("SpCompanion", $"Non-combat decision failed netId={player.NetId}: {ex.Message}");
        }
        finally {
            AiHostContext.Clear();
            _tickRunning = false;
            AiDecisionGate.Exit();
        }
    }

    static GameLoop GetOrCreateLoop(Player player) {
        if (_loops.TryGetValue(player.NetId, out var existing))
            return existing;

        var loop = new GameLoop(
            AiPlayServices.StateProvider,
            AiPlayServices.ActionExecutor,
            StrategyResolver.Resolve(player),
            msg => AiDecisionLog.Record("SpCompanion", $"netId={player.NetId} {msg}")) {
            ActionDelayMs = SettingsStore.Current.AutoPlayDelayMs,
        };
        _loops[player.NetId] = loop;
        return loop;
    }
}
