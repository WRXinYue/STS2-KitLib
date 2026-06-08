using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitLib.Presets;

/// <summary>
/// Generic JSON-backed preset store. Persists a Dictionary&lt;string, T&gt; to disk.
/// </summary>
public sealed class PresetStore<T> where T : class, new() {
    private static readonly JsonSerializerOptions JsonOpts = new() {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _filePath;
    private Dictionary<string, T> _presets = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, T> All => _presets;

    public PresetStore(string filePath) {
        _filePath = filePath;
    }

    public void Load() {
        try {
            if (!File.Exists(_filePath)) { _presets.Clear(); return; }
            var json = File.ReadAllText(_filePath);
            _presets = JsonSerializer.Deserialize<Dictionary<string, T>>(json, JsonOpts)
                ?? new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"PresetStore load failed ({_filePath}): {ex.Message}");
            _presets = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void Save() {
        try {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_presets, JsonOpts);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"PresetStore save failed ({_filePath}): {ex.Message}");
        }
    }

    public bool TryGet(string name, out T preset) {
        return _presets.TryGetValue(name, out preset!);
    }

    public void Set(string name, T preset) {
        _presets[name] = preset;
        Save();
    }

    public bool Delete(string name) {
        if (!_presets.Remove(name)) return false;
        Save();
        return true;
    }

    public bool Rename(string oldName, string newName) {
        if (!_presets.Remove(oldName, out var preset)) return false;
        _presets[newName] = preset;
        Save();
        return true;
    }

    public string Serialize(T preset) {
        return JsonSerializer.Serialize(preset, JsonOpts);
    }

    public T? Deserialize(string json) {
        try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
        catch { return null; }
    }
}
