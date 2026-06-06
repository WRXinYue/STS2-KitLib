using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DevMode.AI;
using DevMode.AI.AutoPlay.Scoring;
using DevMode.AI.Sts2;
using DevMode.AI.Core.Schema;

namespace DevMode.AI.Core;

/// <summary>
/// Generic AI game loop: observe → decide → act.
/// Reusable across different games and AI strategies.
/// </summary>
public sealed class GameLoop
{
    private readonly IGameStateProvider _state;
    private readonly IGameActionExecutor _executor;
    private readonly IDecisionMaker _decisionMaker;
    private readonly Action<string> _log;
    private bool _running;
    private bool _endTurnPending;
    private string? _lastFingerprint;
    private DateTime _lastActionUtc = DateTime.MinValue;
    private string? _repeatFailFingerprint;
    private int _repeatFailCount;

    public int ActionDelayMs { get; set; } = 800;

    public GameLoop(
        IGameStateProvider state,
        IGameActionExecutor executor,
        IDecisionMaker decisionMaker,
        Action<string> log)
    {
        _state = state;
        _executor = executor;
        _decisionMaker = decisionMaker;
        _log = log;
    }

    public void ResetDedupeState() {
        _endTurnPending = false;
        _lastFingerprint = null;
        _lastActionUtc = DateTime.MinValue;
        _repeatFailFingerprint = null;
        _repeatFailCount = 0;
        PotionScorer.ResetTurnTracking();
    }

    /// <summary>
    /// Called when the game reaches a decision point (e.g. start of player turn,
    /// reward screen shown, map opened, etc.).
    /// </summary>
    public async Task OnDecisionPointAsync(GamePhase? phaseOverride = null)
    {
        if (_running) return;
        _running = true;

        try
        {
            var phase = phaseOverride ?? _state.CurrentPhase;
            // #region agent log
            DbgSessionLog.Write("C", "GameLoop.OnDecisionPointAsync", "enter", new {
                phase = phase.ToString(),
                alreadyRunning = _running,
            });
            // #endregion
            if (phase == GamePhase.None) {
                // #region agent log
                DbgSessionLog.Write("C", "GameLoop.OnDecisionPointAsync", "skip phase none", null);
                // #endregion
                return;
            }

            JsonObject snapshot;
            try {
                snapshot = _state is Sts2StateProvider sp
                    ? await sp.TakeSnapshotAsync()
                    : _state.TakeSnapshot() ?? new JsonObject();
                // #region agent log
                if (phase == GamePhase.Combat) {
                    DbgSessionLog.Write("G", "GameLoop.OnDecisionPointAsync", "snapshot ok", new {
                        hand = snapshot["combat"]?["hand"]?.AsArray()?.Count ?? 0,
                        enemies = snapshot["combat"]?["enemies"]?.AsArray()?.Count ?? 0,
                    });
                }
                // #endregion
            }
            catch (Exception snapEx) {
                // #region agent log
                DbgSessionLog.Write("G", "GameLoop.OnDecisionPointAsync", "snapshot error", new {
                    phase = phase.ToString(),
                    type = snapEx.GetType().Name,
                    message = snapEx.Message,
                });
                // #endregion
                if (phase == GamePhase.Combat)
                    return;
                throw;
            }
            if (snapshot == null)
            {
                if (phase == GamePhase.Combat)
                {
                    _log("GameLoop: Could not capture snapshot.");
                    return;
                }

                snapshot = new JsonObject();
            }

            var inCombat = IsCombatContext(phase, snapshot);
            if (!inCombat)
                _endTurnPending = false;

            if (inCombat && snapshot["combat"]?["hand"] == null) {
                // #region agent log
                DbgSessionLog.Write("G", "GameLoop.OnDecisionPointAsync", "combat not ready", new {
                    hasCombat = snapshot["combat"] != null,
                });
                // #endregion
                return;
            }

            if (ShouldSkipCombatPoll(phase, snapshot)) {
                // #region agent log
                DbgSessionLog.Write("D", "GameLoop.OnDecisionPointAsync", "skip combat poll", new {
                    endTurnPending = _endTurnPending,
                    snapshotPlayPhase = ReadSnapshotBool(snapshot, "combat", "isPlayPhaseActive"),
                    livePlayPhase = Sts2CombatCompat.IsCombatPlayPhaseActive(),
                });
                // #endregion
                return;
            }

            var decidePhase = inCombat && phase is GamePhase.Unknown or GamePhase.Combat
                ? GamePhase.Combat
                : phase;

            GameAction action;
            try {
                action = await _decisionMaker.DecideAsync(snapshot, decidePhase);
            }
            catch (Exception decideEx) {
                // #region agent log
                DbgSessionLog.Write("F", "GameLoop.OnDecisionPointAsync", "decide error", new {
                    phase = decidePhase.ToString(),
                    type = decideEx.GetType().Name,
                    message = decideEx.Message,
                });
                // #endregion
                throw;
            }

            // #region agent log
            DbgSessionLog.Write("F", "GameLoop.OnDecisionPointAsync", "decided", new {
                phase = decidePhase.ToString(),
                action = action.Type.ToString(),
                target = action.TargetIndex,
            });
            // #endregion

            var fingerprint = $"{phase}:{action.Type}:{action.TargetIndex}:{action.SecondaryIndex}";
            if (IsDuplicateAction(fingerprint)) {
                // #region agent log
                DbgSessionLog.Write("D", "GameLoop.OnDecisionPointAsync", "skip duplicate", new { fingerprint });
                // #endregion
                return;
            }

            _log($"GameLoop: Phase={phase} Action={action.Type} " +
                 $"Target={action.TargetIndex} Reason=[{action.Reason}]");

            AiHudState.Publish(decidePhase, action);

            if (ShouldDelayBeforeAction(action))
                await Task.Delay(ActionDelayMs);

            var result = await _executor.ExecuteAsync(action);
            // #region agent log
            DbgSessionLog.Write("E", "GameLoop.OnDecisionPointAsync", "executed", new {
                action = action.Type.ToString(),
                success = result.Success,
                message = result.Message,
            });
            // #endregion
            if (!result.Success) {
                _log($"GameLoop: Action failed — {result.Message}");
                if (decidePhase == GamePhase.Combat && action.Type == ActionType.EndTurn) {
                    if (!IsTransientCombatFailure(result.Message))
                        _endTurnPending = true;
                }
                else if (decidePhase == GamePhase.Combat && await TryRecoverFromRepeatedFailureAsync(fingerprint))
                    return;
                return;
            }

            _repeatFailFingerprint = null;
            _repeatFailCount = 0;
            _lastFingerprint = fingerprint;
            _lastActionUtc = DateTime.UtcNow;

            if (decidePhase == GamePhase.Combat && action.Type == ActionType.EndTurn)
                _endTurnPending = true;

            if (decidePhase == GamePhase.Combat && action.Type == ActionType.UsePotion)
                PotionScorer.NotifyPotionUsed();
        }
        catch (Exception ex)
        {
            // #region agent log
            DbgSessionLog.Write("F", "GameLoop.OnDecisionPointAsync", "error", new {
                type = ex.GetType().Name,
                message = ex.Message,
            });
            // #endregion
            _log($"GameLoop: Error — {ex.Message}");
        }
        finally
        {
            _running = false;
        }
    }

    bool ShouldSkipCombatPoll(GamePhase phase, JsonObject snapshot) {
        if (!IsCombatContext(phase, snapshot)) return false;

        // Snapshot was captured on the main thread moments ago; prefer it over live reads from the poll thread.
        var playPhase = ReadSnapshotBool(snapshot, "combat", "isPlayPhaseActive");
        if (playPhase == false) {
            _endTurnPending = false;
            return true;
        }

        return _endTurnPending;
    }

    static bool IsCombatContext(GamePhase phase, JsonObject snapshot) {
        if (phase == GamePhase.Combat)
            return true;

        // PlayerCombatState lingers in snapshots after combat ends; only treat as active
        // combat while the play phase is still running (player can act).
        var combat = snapshot["combat"]?.AsObject();
        return combat?["isPlayPhaseActive"]?.GetValue<bool>() == true;
    }

    bool IsDuplicateAction(string fingerprint) {
        if (_lastFingerprint == null) return false;
        if (_lastFingerprint != fingerprint) return false;
        // CollectReward reuses the same action; draining is one executor call now.
        if (_lastFingerprint.Contains(":CollectReward:", StringComparison.Ordinal))
            return false;
        return (DateTime.UtcNow - _lastActionUtc).TotalMilliseconds < 2000;
    }

    static bool ShouldDelayBeforeAction(GameAction action) =>
        action.Type is ActionType.PlayCard or ActionType.EndTurn or ActionType.UsePotion;

    static bool IsTransientCombatFailure(string? message) =>
        message != null && (
            message.Contains("Not in play phase", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Not in combat", StringComparison.OrdinalIgnoreCase));

    static bool? ReadSnapshotBool(JsonObject snapshot, string section, string key) {
        try {
            return snapshot[section]?[key]?.GetValue<bool>();
        }
        catch {
            return null;
        }
    }

    async Task<bool> TryRecoverFromRepeatedFailureAsync(string fingerprint) {
        if (_repeatFailFingerprint == fingerprint)
            _repeatFailCount++;
        else {
            _repeatFailFingerprint = fingerprint;
            _repeatFailCount = 1;
        }

        if (_repeatFailCount < 3)
            return false;

        _log("GameLoop: repeated play failure — ending turn");
        _repeatFailFingerprint = null;
        _repeatFailCount = 0;

        var endTurn = new GameAction { Type = ActionType.EndTurn, Reason = "Fallback after repeated failure" };
        var result = await _executor.ExecuteAsync(endTurn);
        if (!result.Success) {
            _log($"GameLoop: Action failed — {result.Message}");
            _endTurnPending = true;
            return true;
        }

        _lastFingerprint = $"{GamePhase.Combat}:{ActionType.EndTurn}:-1:-1";
        _lastActionUtc = DateTime.UtcNow;
        _endTurnPending = true;
        return true;
    }
}
