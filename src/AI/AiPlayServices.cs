using KitLib.AI.Sts2;

namespace KitLib.AI;

/// <summary>Shared auto-play state provider and action executor.</summary>
internal static class AiPlayServices {
    public static Sts2StateProvider StateProvider { get; } = new();

    public static Sts2ActionExecutor ActionExecutor { get; } =
        new(StateProvider, msg => MainFile.Logger.Info($"[AiHost] {msg}"));
}
