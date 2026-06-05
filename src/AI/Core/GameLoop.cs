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

            var inCombat = IsCombatContext(phase, snapshot);
            if (!inCombat)
                _endTurnPending = false;

            if (ShouldSkipCombatPoll(phase, snapshot))
                return;

            var decidePhase = inCombat && phase is GamePhase.Unknown or GamePhase.Combat
                ? GamePhase.Combat
                : phase;

            var action = await _decisionMaker.DecideAsync(snapshot, decidePhase);

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
                if (decidePhase == GamePhase.Combat && action.Type == ActionType.EndTurn)
                    _endTurnPending = true;
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
        if (!IsCombatContext(phase, snapshot)) return false;

        var combat = snapshot["combat"]?.AsObject();
        if (combat?["isPlayPhaseActive"]?.GetValue<bool>() == false) {
            _endTurnPending = false;
            return true;
        }

        return _endTurnPending;
    }

    static bool IsCombatContext(GamePhase phase, JsonObject snapshot) =>
        phase == GamePhase.Combat || snapshot["combat"]?.AsObject() != null;

    bool IsDuplicateAction(string fingerprint) {
        if (_lastFingerprint == null) return false;
        if (_lastFingerprint != fingerprint) return false;
        return (DateTime.UtcNow - _lastActionUtc).TotalMilliseconds < 2000;
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
