using System;
using System.Threading;
using System.Threading.Tasks;
using DevMode.AI.Planning;
using DevMode.AI.AutoPlay.Strategies;
using DevMode.AI.Core;
using DevMode.AI.Core.Schema;
using DevMode.AI.Sts2;
using DevMode.Multiplayer.Cheat;
using DevMode.Settings;
using DevMode.UI;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;

namespace DevMode.AI.AutoPlay;

/// <summary>Rule-based autonomous play loop (ported from STS2-AI, no LLM).</summary>
internal sealed class AiPlayModule {
    public static AiPlayModule Instance { get; } = new();

    GameLoop? _loop;
    CancellationTokenSource? _cts;
    IDisposable? _cardSelectorScope;

    public bool IsRunning => _cts != null;

    public static bool IsAutoPlayAllowed => !MpCheatSession.InMultiplayerRun;

    public void OnRunStarted() {
        if (MpCheatSession.InMultiplayerRun) {
            DisableMultiplayerAutoPlay();
            return;
        }
        if (!SettingsStore.Current.AutoPlayEnabled) return;
        StartLoop();
    }

    public void OnRunEnded() {
        StopLoop();
        AiDecisionLog.Clear();
        AiHudState.Clear();
        NextFightDeckEvaluator.ClearCache();
        MapPathPlanner.ClearCache();
        AiHudOverlayUI.SyncState();
    }

    public void StartLoop() {
        if (!IsAutoPlayAllowed) {
            DisableMultiplayerAutoPlay();
            return;
        }

        StopLoop();

        _cardSelectorScope = CardSelectCmd.UseSelector(
            new AiCombatCardSelector(AiPlayServices.StateProvider));

        var strategy = AiPlayServices.StateProvider.TryGetRunAndPlayer(out _, out var me)
            ? StrategyResolver.Resolve(me)
            : StrategyResolver.Resolve(0, null);

        _loop = new GameLoop(
            AiPlayServices.StateProvider,
            AiPlayServices.ActionExecutor,
            strategy,
            msg => AiDecisionLog.Record("AutoPlay", msg)) {
            ActionDelayMs = SettingsStore.Current.AutoPlayDelayMs,
        };

        _cts = new CancellationTokenSource();
        TaskHelper.RunSafely(RunPollLoop(_cts.Token));
        MainFile.Logger.Info(
            $"[AiHost] Started delay={SettingsStore.Current.AutoPlayDelayMs}ms poll={AiPlayConfig.PollIntervalMs}ms");
        AiDecisionLog.Record("AiHost", "AutoPlay loop started.");
        AiHudOverlayUI.SyncState();
        Callable.From(() => AiHudOverlayUI.SyncState()).CallDeferred();
    }

    public void StopLoop() {
        _cardSelectorScope?.Dispose();
        _cardSelectorScope = null;
        _loop?.ResetDedupeState();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _loop = null;
        AiHudOverlayUI.SyncState();
    }

    public void OnDecisionPoint(GamePhase phase) {
        if (!IsAutoPlayAllowed || _loop == null || !SettingsStore.Current.AutoPlayEnabled) return;
        TaskHelper.RunSafely(_loop.OnDecisionPointAsync(phase));
    }

    static void DisableMultiplayerAutoPlay() {
        if (SettingsStore.Current.AutoPlayEnabled) {
            SettingsStore.Current.AutoPlayEnabled = false;
            SettingsStore.Save();
        }
        Instance.StopLoop();
        MainFile.Logger.Info("[AiHost] AutoPlay disabled in multiplayer (use Host AI Teammate for phantom peers only).");
    }

    async Task RunPollLoop(CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            try {
                if (!IsAutoPlayAllowed) {
                    StopLoop();
                    break;
                }
                if (_loop != null && AiPlayServices.StateProvider.IsRunActive) {
                    var phase = AiPlayServices.StateProvider.CurrentPhase;
                    if (phase != GamePhase.None)
                        await _loop.OnDecisionPointAsync(phase);
                }

                await Task.Delay(AiPlayConfig.PollIntervalMs, ct);
            }
            catch (OperationCanceledException) {
                break;
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[AiHost] Poll error — {ex.Message}");
                await Task.Delay(1000, ct);
            }
        }
    }
}
