using System;
using KitLib.AI.Core.Schema;

namespace KitLib.AI;

/// <summary>Last AI decision for in-game HUD display.</summary>
public sealed record AiHudDecision(
    GamePhase Phase,
    ActionType Action,
    string Reason,
    int TargetIndex,
    int SecondaryIndex,
    DateTime Utc);

public static class AiHudState {
    static readonly object Gate = new();
    static AiHudDecision? _last;

    public static AiHudDecision? Last {
        get {
            lock (Gate)
                return _last;
        }
    }

    public static void Publish(GamePhase phase, GameAction action) {
        lock (Gate) {
            _last = new AiHudDecision(
                phase,
                action.Type,
                action.Reason ?? "",
                action.TargetIndex,
                action.SecondaryIndex,
                DateTime.UtcNow);
        }
    }

    public static void Clear() {
        lock (Gate)
            _last = null;
    }
}
