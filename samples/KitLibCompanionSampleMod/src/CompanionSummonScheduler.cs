extern alias KitLibCore;

using KitLibCore::KitLib.Companion;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Runs;

namespace KitLibCompanionSampleMod;

/// <summary>
///     Defers companion spawn until the first map point is recorded (KitLib SP companion guard).
/// </summary>
internal static class CompanionSummonScheduler {
    static bool _pending;
    static RunManager? _hookedManager;

    internal static void Initialize() => EnsureRoomHook();

    internal static void RequestSummonFromRelic() {
        _pending = true;
        EnsureRoomHook();
        TrySummon();
    }

    static void EnsureRoomHook() {
        var runManager = RunManager.Instance;
        if (runManager == null)
            return;

        if (ReferenceEquals(_hookedManager, runManager))
            return;

        if (_hookedManager != null)
            _hookedManager.RoomEntered -= OnRoomEntered;

        runManager.RoomEntered += OnRoomEntered;
        _hookedManager = runManager;
    }

    static void OnRoomEntered() {
        if (_pending)
            TrySummon();
    }

    static void TrySummon() {
        EnsureRoomHook();
        if (!_pending || !CompanionBridge.IsAvailable)
            return;

        if (RunManager.Instance?.IsInProgress != true)
            return;

        var state = RunManager.Instance.DebugOnlyGetState();
        if (state == null)
            return;

        var local = LocalContext.GetMe(state.Players) ?? state.Players.FirstOrDefault();
        if (local == null)
            return;

        var result = CompanionBridge.TrySummon(new CompanionSpawnRequest(
            ModelDb.Character<Ironclad>(),
            UnlockState: local.UnlockState,
            EnableAiTeammate: false,
            MirrorMapVotes: false));

        if (result.Ok) {
            _pending = false;
            return;
        }

        if (result.Error is not { } err || !err.StartsWith("NotReadyYet", StringComparison.Ordinal))
            _pending = false;
    }
}
