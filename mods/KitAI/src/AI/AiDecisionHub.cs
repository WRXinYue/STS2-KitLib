using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using KitLib.AI.Core.Schema;

namespace KitLib.AI;

/// <summary>Publishes structured AI decision snapshots for Dev Viewer and tooling.</summary>
public static class AiDecisionHub {
    static readonly object Gate = new();
    static AiDecisionLiveDto? _latest;
    static string? _latestJson;
    static long _revision;

    public static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static event Action? Changed;

    public static long Revision {
        get {
            lock (Gate)
                return _revision;
        }
    }

    public static AiDecisionLiveDto? Latest {
        get {
            lock (Gate)
                return _latest;
        }
    }

    public static void Publish(JsonObject snapshot, GamePhase phase, GameAction action) {
        var live = AiDecisionViewerModel.BuildLive(snapshot, phase, action);
        string json = JsonSerializer.Serialize(live, JsonOptions);

        lock (Gate) {
            _latest = live;
            _latestJson = json;
            _revision++;
        }

        Changed?.Invoke();
    }

    public static void TouchDecisionLog() {
        lock (Gate) {
            if (_latest?.Active == null)
                return;

            var log = AiDecisionLog.Snapshot();
            var tail = log.Count <= 48 ? log : log.Skip(log.Count - 48).ToList();
            var active = _latest.Active with { DecisionLog = tail };
            _latest = _latest with { Active = active };
            _latestJson = JsonSerializer.Serialize(_latest, JsonOptions);
            _revision++;
        }

        Changed?.Invoke();
    }

    public static bool TryReadJson(out string json) {
        lock (Gate) {
            if (string.IsNullOrEmpty(_latestJson)) {
                json = "";
                return false;
            }

            json = _latestJson;
            return true;
        }
    }

    public static void Clear() {
        lock (Gate) {
            _latest = null;
            _latestJson = null;
            _revision = 0;
        }

        Changed?.Invoke();
    }
}
