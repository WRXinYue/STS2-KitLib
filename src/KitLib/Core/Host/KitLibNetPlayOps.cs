namespace KitLib.Host;

/// <summary>Combat-queue hooks for satellites (DevMode harness).</summary>
public static class KitLibNetPlayOps {
    public static Action<ulong>? OnCombatActionFinished { get; set; }
    public static Action? OnRunEnded { get; set; }
}
