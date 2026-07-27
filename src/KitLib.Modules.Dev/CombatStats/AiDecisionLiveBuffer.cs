using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;
using KitLib.AI;

namespace KitLib.CombatStats;

/// <summary>Persists AI decision snapshots for Dev Viewer late connect.</summary>
internal static class AiDecisionLiveBuffer {
    private static readonly object Gate = new();

    private static AiDecisionLiveDto? _latest;
    private static string? _latestJson;
    private static long _revision;

    public static long Revision {
        get {
            lock (Gate)
                return _revision;
        }
    }

    public static AiDecisionLiveDto? Latest {
        get {
            lock (Gate) {
                if (_latest != null)
                    return _latest;
                return TryLoadFromDisk();
            }
        }
    }

    public static string LiveFilePath =>
        Path.Combine(OS.GetUserDataDir(), "mod_data", "KitLib", "ai-decision-live.json");

    public static void Reset() {
        lock (Gate) {
            _latest = null;
            _latestJson = null;
            _revision = 0;
            try {
                if (File.Exists(LiveFilePath))
                    File.Delete(LiveFilePath);
            }
            catch (Exception ex) {
                KitLog.Warn("AiDecision", $"Live buffer reset failed: {ex.Message}");
            }
        }
    }

    public static void PersistFromHub() {
        if (!AiDecisionHub.TryReadJson(out string json) || string.IsNullOrWhiteSpace(json))
            return;

        lock (Gate) {
            if (json == _latestJson && _latest != null)
                return;

            _latestJson = json;
            _latest = JsonSerializer.Deserialize<AiDecisionLiveDto>(json, AiDecisionHub.JsonOptions);
            _revision = AiDecisionHub.Revision;
            WriteAtomic(json);
        }
    }

    public static bool TryReadJson(out string json) {
        lock (Gate) {
            if (!string.IsNullOrEmpty(_latestJson)) {
                json = _latestJson;
                return true;
            }

            try {
                if (!File.Exists(LiveFilePath)) {
                    json = "";
                    return false;
                }

                json = File.ReadAllText(LiveFilePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) {
                    json = "";
                    return false;
                }

                _latestJson = json;
                _latest = JsonSerializer.Deserialize<AiDecisionLiveDto>(json, AiDecisionHub.JsonOptions);
                if (_revision == 0)
                    _revision = 1;
                return true;
            }
            catch (Exception ex) {
                KitLog.Warn("AiDecision", $"Live buffer read failed: {ex.Message}");
                json = "";
                return false;
            }
        }
    }

    static AiDecisionLiveDto? TryLoadFromDisk() {
        if (!TryReadJson(out string json) || string.IsNullOrWhiteSpace(json))
            return null;

        try {
            return _latest ?? JsonSerializer.Deserialize<AiDecisionLiveDto>(json, AiDecisionHub.JsonOptions);
        }
        catch (Exception ex) {
            KitLog.Warn("AiDecision", $"Live buffer parse failed: {ex.Message}");
            return null;
        }
    }

    static void WriteAtomic(string json) {
        try {
            string path = LiveFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            File.WriteAllText(temp, json, Encoding.UTF8);
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }
        catch (Exception ex) {
            KitLog.Warn("AiDecision", $"Live buffer write failed: {ex.Message}");
        }
    }
}
