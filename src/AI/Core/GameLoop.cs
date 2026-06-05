using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
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
    private bool _awaitingCombatUpdate;
    private string? _preActionCombatFingerprint;
    private string? _lastFingerprint;
    private DateTime _lastActionUtc = DateTime.MinValue;

    const int CombatUpdateTimeoutMs = 5000;

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
        _awaitingCombatUpdate = false;
        _preActionCombatFingerprint = null;
        _lastFingerprint = null;
        _lastActionUtc = DateTime.MinValue;
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
            if (phase == GamePhase.None) return;

            if (phase != GamePhase.Combat) {
                _endTurnPending = false;
                _awaitingCombatUpdate = false;
                _preActionCombatFingerprint = null;
            }

            var snapshot = _state.TakeSnapshot();
            if (snapshot == null)
            {
                if (phase == GamePhase.Combat)
                {
                    _log("GameLoop: Could not capture snapshot.");
                    return;
                }

                snapshot = new JsonObject();
            }

            if (ShouldSkipCombatPoll(phase, snapshot))
                return;

            var action = await _decisionMaker.DecideAsync(snapshot, phase);

            var fingerprint = $"{phase}:{action.Type}:{action.TargetIndex}:{action.SecondaryIndex}";
            if (IsDuplicateAction(fingerprint))
                return;

            _log($"GameLoop: Phase={phase} Action={action.Type} " +
                 $"Target={action.TargetIndex} Reason=[{action.Reason}]");

            if (ActionDelayMs > 0)
                await Task.Delay(ActionDelayMs);

            var result = await _executor.ExecuteAsync(action);
            if (!result.Success) {
                _log($"GameLoop: Action failed — {result.Message}");
                if (phase == GamePhase.Combat && action.Type == ActionType.EndTurn)
                    _endTurnPending = true;
                if (phase == GamePhase.Combat
                    && action.Type is ActionType.PlayCard or ActionType.UsePotion)
                    _awaitingCombatUpdate = false;
                return;
            }

            _lastFingerprint = fingerprint;
            _lastActionUtc = DateTime.UtcNow;

            if (phase == GamePhase.Combat && action.Type == ActionType.EndTurn) {
                _endTurnPending = true;
                _awaitingCombatUpdate = false;
            }
            else if (phase == GamePhase.Combat
                     && action.Type is ActionType.PlayCard or ActionType.UsePotion) {
                _preActionCombatFingerprint = CombatFingerprint.FromSnapshot(snapshot);
                _awaitingCombatUpdate = true;
            }
        }
        catch (Exception ex)
        {
            _log($"GameLoop: Error — {ex.Message}");
        }
        finally
        {
            _running = false;
        }
    }

    bool ShouldSkipCombatPoll(GamePhase phase, JsonObject snapshot) {
        if (phase != GamePhase.Combat) return false;

        var combat = snapshot["combat"]?.AsObject();
        if (combat?["isPlayPhaseActive"]?.GetValue<bool>() == false) {
            _endTurnPending = false;
            _awaitingCombatUpdate = false;
            return true;
        }

        if (_endTurnPending)
            return true;

        if (!_awaitingCombatUpdate || _preActionCombatFingerprint == null)
            return false;

        var elapsed = (DateTime.UtcNow - _lastActionUtc).TotalMilliseconds;
        var fp = CombatFingerprint.FromSnapshot(snapshot);
        if (fp != _preActionCombatFingerprint) {
            _awaitingCombatUpdate = false;
            return false;
        }

        return elapsed < CombatUpdateTimeoutMs;
    }

    bool IsDuplicateAction(string fingerprint) {
        if (_lastFingerprint == null) return false;
        if (_lastFingerprint != fingerprint) return false;
        return (DateTime.UtcNow - _lastActionUtc).TotalMilliseconds < 2000;
    }
}
