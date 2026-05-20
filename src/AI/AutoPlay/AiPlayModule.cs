using System;
using System.Threading;
using System.Threading.Tasks;
using DevMode.AI.AutoPlay.Strategies;
using DevMode.AI.Core;
using DevMode.AI.Core.Schema;
using DevMode.Multiplayer.Cheat;
using DevMode.Settings;
using MegaCrit.Sts2.Core.Helpers;

namespace DevMode.AI.AutoPlay;

/// <summary>Rule-based autonomous play loop (ported from STS2-AI, no LLM).</summary>
internal sealed class AiPlayModule {
    public static AiPlayModule Instance { get; } = new();

    GameLoop? _loop;
    CancellationTokenSource? _cts;

    public bool IsRunning => _cts != null;

    public void OnRunStarted() {
        if (!SettingsStore.Current.AutoPlayEnabled) return;
        if (MpCheatSession.InMultiplayerRun) {
            MainFile.Logger.Info("[AiHost] Skipped auto-start in multiplayer (enable manually if needed).");
            return;
        }
        StartLoop();
    }

    public void OnRunEnded() => StopLoop();

    public void StartLoop() {
        StopLoop();

        _loop = new GameLoop(
            AiPlayServices.StateProvider,
            AiPlayServices.ActionExecutor,
            new SimpleStrategy(),
            msg => MainFile.Logger.Info($"[AutoPlay] {msg}")) {
            ActionDelayMs = SettingsStore.Current.AutoPlayDelayMs,
        };

        _cts = new CancellationTokenSource();
        TaskHelper.RunSafely(RunPollLoop(_cts.Token));
        MainFile.Logger.Info(
            $"[AiHost] Started delay={SettingsStore.Current.AutoPlayDelayMs}ms poll={AiPlayConfig.PollIntervalMs}ms");
    }

    public void StopLoop() {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _loop = null;
    }

    public void OnDecisionPoint(GamePhase phase) {
        if (_loop == null || !SettingsStore.Current.AutoPlayEnabled) return;
        TaskHelper.RunSafely(_loop.OnDecisionPointAsync(phase));
    }

    async Task RunPollLoop(CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            try {
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
