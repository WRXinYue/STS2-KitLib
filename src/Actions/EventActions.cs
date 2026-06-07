using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Actions;

internal static class EventActions {
    public static IEnumerable<EventModel> GetAllEvents() {
        try { return ModelDb.AllEvents.Concat(ModelDb.AllAncients).Distinct(); }
        catch { return Enumerable.Empty<EventModel>(); }
    }

    public static IEnumerable<AncientEventModel> GetAllAncients() {
        try { return ModelDb.AllAncients; }
        catch { return Enumerable.Empty<AncientEventModel>(); }
    }

    /// <summary>
    /// Force-enter an event room, mirroring the game's own EventConsoleCmd.
    /// Requires an active run; silently fails (with a log warning) otherwise.
    /// </summary>
    public static bool TryForceEnterEvent(EventModel eventModel, AncientEventEnterRequest? request = null) {
        try {
            if (!RunManager.Instance.IsInProgress) {
                MainFile.Logger.Warn("[DevMode] ForceEnterEvent: no run in progress.");
                return false;
            }

            if (!RunContext.TryGetRunAndPlayer(out _, out var player)) {
                MainFile.Logger.Warn("[DevMode] ForceEnterEvent: could not get active player.");
                return false;
            }

            ApplyAncientEnterRequest(eventModel, request);

            var mapPointType = eventModel is AncientEventModel
                ? MapPointType.Ancient
                : MapPointType.Unknown;

            player.RunState.AppendToMapPointHistory(mapPointType, RoomType.Event, eventModel.Id);
            TaskHelper.RunSafely(RunManager.Instance.EnterRoom(new EventRoom(eventModel)));
            return true;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[DevMode] ForceEnterEvent failed: {ex.Message}");
            return false;
        }
    }

    private static void ApplyAncientEnterRequest(EventModel eventModel, AncientEventEnterRequest? request) {
        AncientEventDebugSession.ClearPendingDarvBranch();
        if (request?.DarvIncludeDustyTome is bool branch && AncientEventActions.IsDarv(eventModel))
            AncientEventDebugSession.PendingDarvDustyTomeBranch = branch;
    }

    public static string GetEventDisplayName(EventModel evt) {
        try { return evt.Title?.GetFormattedText() ?? ((AbstractModel)evt).Id.Entry ?? "?"; }
        catch { return ((AbstractModel)evt).Id.Entry ?? "?"; }
    }
}
