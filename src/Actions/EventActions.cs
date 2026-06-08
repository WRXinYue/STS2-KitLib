using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Actions;

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
                MainFile.Logger.Warn("[KitLib] ForceEnterEvent: no run in progress.");
                return false;
            }

            if (!RunContext.TryGetRunAndPlayer(out _, out var player)) {
                MainFile.Logger.Warn("[KitLib] ForceEnterEvent: could not get active player.");
                return false;
            }

            if (eventModel is AncientEventModel ancient
                && request?.PinOptionToken is string pin
                && !AncientEventActions.IsValidChoice(ancient, pin, player)) {
                MainFile.Logger.Warn(
                    $"[KitLib] ForceEnterEvent: invalid ancient choice '{pin}' for current run/deck.");
                return false;
            }

            var mapPointType = eventModel is AncientEventModel
                ? MapPointType.Ancient
                : MapPointType.Unknown;

            var room = CreateEventRoom(eventModel, request);
            player.RunState.AppendToMapPointHistory(mapPointType, RoomType.Event, eventModel.Id);
            TaskHelper.RunSafely(RunManager.Instance.EnterRoom(room));
            return true;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[KitLib] ForceEnterEvent failed: {ex.Message}");
            return false;
        }
    }

    private static EventRoom CreateEventRoom(EventModel eventModel, AncientEventEnterRequest? request) {
        var pin = request?.PinOptionToken;
        if (eventModel is not AncientEventModel || string.IsNullOrWhiteSpace(pin))
            return new EventRoom(eventModel);

        return new EventRoom(eventModel) {
            OnStart = e => {
                if (e is AncientEventModel ancient)
                    ancient.DebugOption = pin.ToUpperInvariant();
            },
        };
    }

    public static string GetEventDisplayName(EventModel evt) {
        try { return evt.Title?.GetFormattedText() ?? ((AbstractModel)evt).Id.Entry ?? "?"; }
        catch { return ((AbstractModel)evt).Id.Entry ?? "?"; }
    }
}
