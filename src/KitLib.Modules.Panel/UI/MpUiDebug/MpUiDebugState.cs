namespace KitLib.UI;

internal enum MpUiDebugScenario {
    None,
    RestSiteFourSame,
    RelicSoloHand,
}

/// <summary>One-shot MP layout debug scenarios triggered from room teleport.</summary>
internal static class MpUiDebugState {
    internal const int RestSitePlayerCount = 4;

    internal static MpUiDebugScenario PendingScenario { get; set; } = MpUiDebugScenario.None;
}
