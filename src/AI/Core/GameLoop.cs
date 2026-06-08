using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib;
using KitLib.AI.AutoPlay.Scoring;
using KitLib.AI.Sts2;
using KitLib.AI.Core.Schema;

namespace KitLib.AI.Core;

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
    private int _endTurnAtRound = -1;
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
        _endTurnAtRound = -1;
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
        if (_running)
            return;
        _running = true;

        try
        {
            var phase = phaseOverride ?? _state.CurrentPhase;
            if (phase == GamePhase.None)
                return;

            JsonObject snapshot;
            try {
                snapshot = _state is Sts2StateProvider sp
                    ? await sp.TakeSnapshotAsync()
                    : _state.TakeSnapshot() ?? new JsonObject();
            }
            catch (Exception) {
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

            if (inCombat && !IsCombatSnapshotReady(snapshot))
                return;

            if (ShouldSkipCombatPoll(phase, snapshot, out _))
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

            AiHudState.Publish(decidePhase, action);

            if (ShouldDelayBeforeAction(action))
                await Task.Delay(ActionDelayMs);

            var result = await _executor.ExecuteAsync(action);
            if (!result.Success) {
                _log($"GameLoop: Action failed — {result.Message}");
                if (decidePhase == GamePhase.Combat && action.Type == ActionType.EndTurn) {
                    if (!IsTransientCombatFailure(result.Message))
                        MarkEndTurnPending();
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
                MarkEndTurnPending();

            if (decidePhase == GamePhase.Combat && action.Type == ActionType.UsePotion)
                PotionScorer.NotifyPotionUsed();
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

    bool ShouldSkipCombatPoll(GamePhase phase, JsonObject snapshot, out string? reason) {
        reason = null;
        if (!IsCombatContext(phase, snapshot))
            return false;

        // Snapshot was captured on the main thread moments ago; prefer it over live reads from the poll thread.
        var playPhase = ReadSnapshotBool(snapshot, "combat", "isPlayPhaseActive");
        var livePlayPhase = Sts2CombatCompat.IsCombatPlayPhaseActive();
        if (playPhase == false || !livePlayPhase) {
            _endTurnPending = false;
            _endTurnAtRound = -1;
            reason = "playPhaseInactive";
            return true;
        }

        if (_endTurnPending && TryClearEndTurnPendingForNewRound(snapshot))
            return false;

        if (_endTurnPending) {
            reason = "endTurnPending";
            return true;
        }

        return false;
    }

    void MarkEndTurnPending() {
        _endTurnPending = true;
        _endTurnAtRound = Sts2CombatCompat.GetCombatRoundNumber();
    }

    bool TryClearEndTurnPendingForNewRound(JsonObject snapshot) {
        if (!_endTurnPending || _endTurnAtRound < 0)
            return false;

        var round = ReadSnapshotInt(snapshot, "combat", "turnNumber");
        if (round == null || round.Value <= _endTurnAtRound)
            return false;

        _endTurnPending = false;
        _endTurnAtRound = -1;
        return true;
    }

    static bool IsCombatContext(GamePhase phase, JsonObject snapshot) {
        if (phase == GamePhase.Combat)
            return true;

        // PlayerCombatState lingers in snapshots after combat ends; only treat as active
        // combat while the play phase is still running (player can act).
        var combat = snapshot["combat"]?.AsObject();
        return combat?["isPlayPhaseActive"]?.GetValue<bool>() == true;
    }

    static bool IsCombatSnapshotReady(JsonObject snapshot) {
        var combat = snapshot["combat"]?.AsObject();
        if (combat == null)
            return false;

        var hand = combat["hand"]?.AsArray();
        var enemies = combat["enemies"]?.AsArray();
        if (hand == null || enemies == null || enemies.Count == 0)
            return false;

        // Empty hand during an active play phase means capture ran before draw finished.
        if (combat["isPlayPhaseActive"]?.GetValue<bool>() == true && hand.Count == 0)
            return false;

        return true;
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

    static int? ReadSnapshotInt(JsonObject snapshot, string section, string key) {
        try {
            return snapshot[section]?[key]?.GetValue<int>();
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
            MarkEndTurnPending();
            return true;
        }

        _lastFingerprint = $"{GamePhase.Combat}:{ActionType.EndTurn}:-1:-1";
        _lastActionUtc = DateTime.UtcNow;
        MarkEndTurnPending();
        return true;
    }
}
