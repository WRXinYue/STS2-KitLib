namespace KitLib.CombatStats;

/// <summary>Thread-local stack of effect sources set by Harmony patches around game commands.</summary>
internal static class CombatStatsSourceContext {
    [ThreadStatic]
    private static Stack<CombatStatSource>? _stack;

    public static void Push(CombatStatSource source) {
        _stack ??= new Stack<CombatStatSource>();
        _stack.Push(source);
    }

    public static void Pop() {
        _stack?.TryPop(out _);
    }

    public static void Clear() => _stack?.Clear();

    public static bool TryPeek(out CombatStatSource source) {
        if (_stack is { Count: > 0 }) {
            source = _stack.Peek();
            return source.IsKnown;
        }

        source = CombatStatSource.Unknown;
        return false;
    }
}
