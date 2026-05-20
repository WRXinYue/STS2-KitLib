namespace DevMode.AI;

/// <summary>Registers auto-play lifecycle (Harmony patches in <see cref="Sts2.Patches.AiPlayRunPatches"/>).</summary>
public static class AiPlayInitializer {
    public static void Initialize() {
        MainFile.Logger.Info("[AiHost] Module ready.");
    }
}
