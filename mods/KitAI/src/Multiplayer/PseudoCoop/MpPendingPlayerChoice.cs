using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Multiplayer;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>Tracks expected remote choice shape between ReserveChoiceId and WaitForRemoteChoice.</summary>
internal static class MpPendingPlayerChoice {
    readonly record struct Pending(PlayerChoiceOptions Options);

    static readonly Dictionary<(ulong NetId, uint ChoiceId), Pending> _pending = [];

    internal static void Register(ulong netId, uint choiceId, PlayerChoiceOptions options) {
        if (netId == 0) return;
        _pending[(netId, choiceId)] = new Pending(options);
    }

    internal static bool TryConsume(ulong netId, uint choiceId, out PlayerChoiceOptions options) {
        options = PlayerChoiceOptions.None;
        if (!_pending.Remove((netId, choiceId), out var pending))
            return false;

        options = pending.Options;
        return true;
    }

    internal static void Clear() => _pending.Clear();
}
