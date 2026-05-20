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
            // Unknown is handled by strategies (e.g. AdvanceOverlay for unrecognized overlays).
            if (phase == GamePhase.None) return;

            var snapshot = _state.TakeSnapshot();
            if (snapshot == null)
            {
                // Combat needs a real snapshot; overlay / map phases can still decide with defaults.
                if (phase == GamePhase.Combat)
                {
                    _log("GameLoop: Could not capture snapshot.");
                    return;
                }

                snapshot = new JsonObject();
            }

            var action = await _decisionMaker.DecideAsync(snapshot, phase);
            _log($"GameLoop: Phase={phase} Action={action.Type} " +
                 $"Target={action.TargetIndex} Reason=[{action.Reason}]");

            if (ActionDelayMs > 0)
                await Task.Delay(ActionDelayMs);

            var result = await _executor.ExecuteAsync(action);
            if (!result.Success)
                _log($"GameLoop: Action failed — {result.Message}");
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
}
