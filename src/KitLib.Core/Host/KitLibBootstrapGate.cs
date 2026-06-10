namespace KitLib.Host;

/// <summary>KitLib runtime phase; gates APIs that hard-crash on STS2 stable during the wrong window.</summary>
public enum KitLibBootstrapPhase {
    /// <summary>Core and satellite initialization; <see cref="MainFile.Logger"/> and path pin are allowed.</summary>
    ModLoad,

    /// <summary>Main-menu scene-ready Dev bootstrap; avoid Logger and HTTP listeners.</summary>
    SceneReady,

    /// <summary>Dev bootstrap finished; normal runtime APIs are allowed.</summary>
    Interactive,
}

public static class KitLibBootstrapGate {
    public static KitLibBootstrapPhase Phase { get; private set; } = KitLibBootstrapPhase.ModLoad;

    public static bool CanUseMainFileLogger =>
        Phase is KitLibBootstrapPhase.ModLoad or KitLibBootstrapPhase.Interactive;

    public static bool CanResolveGodotDataPaths => Phase == KitLibBootstrapPhase.ModLoad;

    public static bool CanStartHttpListener => Phase == KitLibBootstrapPhase.Interactive;

    internal static void EnterSceneReadyBootstrap() {
        if (Phase == KitLibBootstrapPhase.ModLoad)
            Phase = KitLibBootstrapPhase.SceneReady;
    }

    internal static void EnterInteractive() => Phase = KitLibBootstrapPhase.Interactive;
}
