using System.Collections.Generic;
using System.Text.Json.Nodes;
using KitLib.AI.Core.Schema;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.AI.Core;

public static class AiSnapshotHub {
    static readonly List<IAiSnapshotContributor> Contributors = [];

    public static void Register(IAiSnapshotContributor contributor) {
        Contributors.Add(contributor);
        MainFile.Logger.Info($"[AiSnapshot] Contributor registered key={contributor.ExtensionKey}.");
    }

    public static void Enrich(JsonObject snapshot, Player player, GamePhase phase) {
        if (Contributors.Count == 0)
            return;

        snapshot["extensions"] ??= new JsonObject();

        foreach (var contributor in Contributors) {
            try {
                contributor.Enrich(snapshot, player, phase);
            }
            catch (System.Exception ex) {
                MainFile.Logger.Warn($"[AiSnapshot] Contributor {contributor.ExtensionKey} failed: {ex.Message}");
            }
        }
    }
}
