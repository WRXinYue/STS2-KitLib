using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KitLib.AI;
using KitLib.AI.Core;
using KitLib.AI.Core.Schema;
using KitLib.Companion;
using KitLib.Multiplayer.Cheat;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>Runs <see cref="GameLoop"/> for host-driven companions during non-combat phases.</summary>
internal static class CompanionDecisionHost {
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

    public static bool IsEnabled => LanAiOwnership.ShouldRunCompanionHost;

    public static void OnRunEnded() {
        _tickRunning = false;
        _loops.Clear();
        AiDecisionGate.Reset();
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

        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null) {
            AiDecisionGate.Exit();
            return;
        }

        foreach (var player in SimulatedPeerRegistry.GetMpAiTeammateTargets()) {
            if (!ShouldRunNonCombatFor(player, phase)) continue;

            _tickRunning = true;
            TaskHelper.RunSafely(RunDecisionAsync(player, phase));
            return;
        }

        AiDecisionGate.Exit();
    }

    static bool ShouldRunNonCombatFor(Player player, GamePhase phase) {
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
            MainFile.Logger.Warn($"[CompanionAi] Decision failed netId={player.NetId}: {ex.Message}");
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
            msg => AiDecisionLog.Record("Companion", $"netId={player.NetId} {msg}")) {
            ActionDelayMs = SettingsStore.Current.AutoPlayDelayMs,
        };
        _loops[player.NetId] = loop;
        return loop;
    }
}
