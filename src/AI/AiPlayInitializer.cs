namespace KitLib.AI;

/// <summary>Registers auto-play lifecycle (Harmony patches in <see cref="Sts2.Patches.AiPlayRunPatches"/>).</summary>
public static class AiPlayInitializer {
    public static void Initialize() {
        // Catalog + vanilla packs register after ModelDb.Init (see ModelDbInitPatch).
        MainFile.Logger.Info("[AiHost] Module ready (AI knowledge deferred until ModelDb.Init).");
    }
}
