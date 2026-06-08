using System.Collections.Generic;

namespace KitLib.Scripts;

/// <summary>
/// Per-combat variable storage for SpireScratch scripts.
/// Variables persist within a single combat; call <see cref="Reset"/> on combat start.
/// </summary>
internal static class ScriptVariableStore {
    private static readonly Dictionary<string, int> _vars = new();

    public static int Get(string name) =>
        _vars.TryGetValue(name, out var v) ? v : 0;

    public static void Set(string name, int value) =>
        _vars[name] = value;

    public static void Increment(string name, int delta) {
        _vars.TryGetValue(name, out var current);
        _vars[name] = current + delta;
    }

    public static IReadOnlyDictionary<string, int> All => _vars;

    public static void Reset() => _vars.Clear();
}
