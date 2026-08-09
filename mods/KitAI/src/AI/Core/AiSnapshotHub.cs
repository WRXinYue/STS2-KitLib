using System.Text.Json.Nodes;
using KitLib.AI.Core.Schema;
using KitLib.Host;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.AI.Core;

public static class AiSnapshotHub {
    public static void Register(IAiSnapshotContributor contributor) =>
        KitLibHost.RegisterSnapshotContributor(contributor);

    public static void Enrich(JsonObject snapshot, Player player, GamePhase phase) =>
        KitLibHost.EnrichSnapshot(snapshot, player, phase);
}
