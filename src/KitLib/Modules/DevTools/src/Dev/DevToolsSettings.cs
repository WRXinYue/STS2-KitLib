using System;
using System.IO;
using System.Text.Json;
using Godot;

namespace KitLib.Replay;

internal static class DevToolsSettings {
    internal const int DefaultKeepCount = 5;
    internal const int MinKeepCount = 1;
    internal const int MaxKeepCount = 9999;

    static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    static int _keepCount = DefaultKeepCount;
    static bool _loaded;

    internal static int RunReplayKeepCount {
        get {
            EnsureLoaded();
            return _keepCount;
        }
    }

    internal static void SetRunReplayKeepCount(int count) {
        EnsureLoaded();
        int next = Math.Clamp(count, MinKeepCount, MaxKeepCount);
        if (next == _keepCount)
            return;
        _keepCount = next;
        Save();
        RunReplayRetention.Prune();
    }

    static void EnsureLoaded() {
        if (_loaded)
            return;
        _loaded = true;
        try {
            string path = FilePath();
            if (!File.Exists(path))
                return;
            var data = JsonSerializer.Deserialize<FileModel>(File.ReadAllText(path), JsonOpts);
            if (data?.RunReplayKeepCount is int n)
                _keepCount = Math.Clamp(n, MinKeepCount, MaxKeepCount);
        }
        catch (Exception ex) {
            GD.PrintErr($"[KitLib.DevTools] Failed to load settings: {ex.Message}");
        }
    }

    static void Save() {
        try {
            string path = FilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var data = new FileModel { RunReplayKeepCount = _keepCount };
            File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOpts));
        }
        catch (Exception ex) {
            GD.PrintErr($"[KitLib.DevTools] Failed to save settings: {ex.Message}");
        }
    }

    static string FilePath() =>
        Path.Combine(OS.GetUserDataDir(), "KitLib", "devtools-settings.json");

    sealed class FileModel {
        public int RunReplayKeepCount { get; set; } = DefaultKeepCount;
    }
}
