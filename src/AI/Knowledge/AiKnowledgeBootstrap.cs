namespace KitLib.AI.Knowledge;

/// <summary>Defers catalog indexing until <see cref="MegaCrit.Sts2.Core.Models.ModelDb.Init"/> completes.</summary>
public static class AiKnowledgeBootstrap {
    static bool _registered;
    static readonly object Gate = new();

    public static void EnsureRegistered() {
        if (_registered) return;
        lock (Gate) {
            if (_registered) return;
            try {
                Characters.Vanilla.VanillaAiBootstrap.Register();
                CodexPriorCatalog.EnsureLoaded();
                _registered = true;
            }
            catch (System.Exception ex) {
                MainFile.Logger.Warn($"[AiKnowledge] Bootstrap failed (will retry): {ex.Message}");
            }
        }
    }

    internal static bool IsRegistered => _registered;
}
