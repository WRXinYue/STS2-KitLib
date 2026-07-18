extern alias KitLibCore;

using KitLibCore::KitLib.Companion;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;

namespace KitLibCompanionSampleMod;

/// <summary>
///     Defers companion spawn until the first map point is recorded (KitLib SP companion guard).
/// </summary>
internal static class CompanionSummonScheduler {
    static bool _pending;
    static IDisposable? _subscription;

    internal static void Initialize() {
        _subscription ??= RitsuLibFramework.SubscribeLifecycle<RoomEnteringEvent>(
            OnRoomEntering,
            replayCurrentState: false);
    }

    internal static void RequestSummonFromRelic() {
        _pending = true;
        TrySummon();
    }

    static void OnRoomEntering(RoomEnteringEvent _) {
        if (_pending)
            TrySummon();
    }

    static void TrySummon() {
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
