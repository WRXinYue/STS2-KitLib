using System.Text.Json.Nodes;
using KitLib.AI.Core;
using KitLib.AI.Core.Schema;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLibSmokeMod.AI;

internal sealed class SmokeSnapshotContributor : IAiSnapshotContributor {
    public static SmokeSnapshotContributor Instance { get; } = new();

    public string ExtensionKey => SmokeAiState.ExtensionKey;

    public void Enrich(JsonObject snapshot, Player player, GamePhase phase) {
        if (!SmokeAiState.IsTarget(player))
            return;

        var extensions = snapshot["extensions"] as JsonObject ?? new JsonObject();
        snapshot["extensions"] = extensions;
        extensions[ExtensionKey] = new JsonObject { ["smoke"] = true };
    }
}
