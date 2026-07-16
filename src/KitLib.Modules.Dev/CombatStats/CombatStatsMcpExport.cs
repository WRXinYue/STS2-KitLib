using System.Linq;
using System.Text.Json.Nodes;

namespace KitLib.CombatStats;

internal static class CombatStatsMcpExport {
    public static JsonObject Capture(int sinceSequence = 0, int maxEvents = 200, int? turn = null) {
        var live = CombatStatsExport.CaptureLive();
        var response = new JsonObject {
            ["isActive"] = live.IsActive,
        };

        if (live.Active == null) {
            response["active"] = null;
            return response;
        }

        var snap = live.Active;
        var events = snap.CombatEvents.AsEnumerable();
        if (sinceSequence > 0)
            events = events.Where(e => e.Sequence > sinceSequence);
        if (turn.HasValue)
            events = events.Where(e => e.Turn == turn.Value);

        var eventList = events
            .OrderBy(e => e.Sequence)
            .Take(maxEvents > 0 ? maxEvents : 200)
            .Select(e => new JsonObject {
                ["sequence"] = e.Sequence,
                ["turn"] = e.Turn,
                ["kind"] = e.Kind,
                ["text"] = e.Text,
                ["amount"] = e.Amount,
                ["actorKey"] = e.ActorKey,
                ["actorSide"] = e.ActorSide,
                ["actorName"] = e.ActorName,
                ["sourceKind"] = e.SourceKind,
                ["sourceKey"] = e.SourceKey,
                ["sourceName"] = e.SourceName,
                ["linkedToCardPlay"] = e.LinkedToCardPlay,
                ["statePhase"] = string.IsNullOrEmpty(e.StatePhase) ? null : e.StatePhase,
            })
            .ToArray();

        response["active"] = new JsonObject {
            ["encounterKey"] = snap.EncounterKey,
            ["isActive"] = snap.IsActive,
            ["maxTurn"] = snap.MaxTurn,
            ["eventCount"] = snap.CombatEvents.Count,
            ["combatEvents"] = new JsonArray(eventList),
            ["liveCreatures"] = JsonNode.Parse(
                System.Text.Json.JsonSerializer.Serialize(snap.LiveCreatures, CombatStatsExport.JsonOptions)),
        };

        if (eventList.Length > 0)
            response["lastSequence"] = eventList[^1]!["sequence"]!.GetValue<int>();

        return response;
    }
}
