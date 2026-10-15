namespace KitLib.UI;

internal enum MpUiDebugScenario {
    None,
    RestSiteFourSame,
    TreasureFourSame,
}

/// <summary>One-shot MP layout debug scenarios triggered from room teleport.</summary>
internal static class MpUiDebugState {
    internal const int DebugPlayerCount = 4;

    internal static MpUiDebugScenario PendingScenario { get; set; } = MpUiDebugScenario.None;

    /// <summary>True after <see cref="MpUiDebugPlayerService"/> spawns NetId 9101+ debug players.</summary>
    internal static bool HasSpawnedDebugPlayers { get; set; }

    /// <summary>Map vote UI must be rebuilt once the map screen is open again.</summary>
    internal static bool PendingMapVoteCleanup { get; set; }
}
