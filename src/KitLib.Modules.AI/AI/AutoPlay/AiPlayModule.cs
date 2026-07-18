using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using KitLib.AI.AutoPlay.Strategies;
using KitLib.AI.Core;
using KitLib.AI.Core.Schema;
using KitLib.AI.Planning;
using KitLib.AI.Sts2;
using KitLib.Host;
using KitLib.Settings;
using KitLib.Singleplayer.Companion;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;

namespace KitLib.AI.AutoPlay;

/// <summary>Rule-based autonomous play loop (ported from STS2-AI, no LLM).</summary>
internal sealed class AiPlayModule {
    public static AiPlayModule Instance { get; } = new();

    GameLoop? _loop;
    CancellationTokenSource? _cts;
    IDisposable? _cardSelectorScope;

    public bool IsRunning => _cts != null;

    public static bool IsAutoPlayAllowed => !MultiplayerRunProbe.InMultiplayerRun;

    public void OnRunStarted() {
        if (MultiplayerRunProbe.InMultiplayerRun) {
            DisableMultiplayerAutoPlay();
            return;
        }
        if (!AiSessionSettings.AutoPlayEnabled) return;
        // LoadRun replaces NGlobalUi; defer until the new scene tree is ready.
        Callable.From(StartLoopIfEnabled).CallDeferred();
    }

    void StartLoopIfEnabled() {
        if (!IsAutoPlayAllowed || !AiSessionSettings.AutoPlayEnabled)
            return;
        StartLoop();
    }

    public void OnRunEnded() {
        StopLoop();
        SpvCompanionAiHost.OnRunEnded();
        AiSessionSettings.ResetRunSession();
        AiDecisionLog.Clear();
        AiHudState.Clear();
        NextFightDeckEvaluator.ClearCache();
        MapPathPlanner.ClearCache();
        KitLibHost.SyncAiHudOverlay?.Invoke();
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
        KitLog.Info("AiHost", $"Started delay={SettingsStore.Current.AutoPlayDelayMs}ms poll={AiPlayConfig.PollIntervalMs}ms");
        AiDecisionLog.Record("AiHost", "AutoPlay loop started.");
        KitLibHost.SyncAiHudOverlay?.Invoke();
        Callable.From(() => KitLibHost.SyncAiHudOverlay?.Invoke()).CallDeferred();
    }

    public void StopLoop() {
        _cardSelectorScope?.Dispose();
        _cardSelectorScope = null;
        _loop?.ResetDedupeState();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _loop = null;
        KitLibHost.SyncAiHudOverlay?.Invoke();
    }

    public void OnDecisionPoint(GamePhase phase) {
        if (!IsAutoPlayAllowed || _loop == null || !AiSessionSettings.AutoPlayEnabled) return;
        TaskHelper.RunSafely(_loop.OnDecisionPointAsync(phase));
    }

    static void DisableMultiplayerAutoPlay() {
        if (AiSessionSettings.AutoPlayEnabled)
            AiSessionSettings.AutoPlayEnabled = false;
        Instance.StopLoop();
        KitLog.Info("AiHost", $"AutoPlay disabled in multiplayer (use Host AI Teammate for phantom peers only).");
    }

    async Task RunPollLoop(CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            try {
                if (!IsAutoPlayAllowed) {
                    StopLoop();
                    break;
                }
                var isRunActive = AiPlayServices.StateProvider.IsRunActive;
                var phase = AiPlayServices.StateProvider.CurrentPhase;
                if (_loop != null && isRunActive) {
                    if (phase != GamePhase.None)
                        await _loop.OnDecisionPointAsync(phase);
                }

                await Task.Delay(AiPlayConfig.PollIntervalMs, ct);
            }
            catch (OperationCanceledException) {
                break;
            }
            catch (Exception ex) {
                KitLog.Warn("AiHost", $"Poll error — {ex.Message}");
                await Task.Delay(1000, ct);
            }
        }
    }
}
