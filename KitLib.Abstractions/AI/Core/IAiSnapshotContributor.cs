using System.Text.Json.Nodes;
using KitLib.AI.Core.Schema;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.AI.Core;

public interface IAiSnapshotContributor {
    string ExtensionKey { get; }
    void Enrich(JsonObject snapshot, Player player, GamePhase phase);
}
