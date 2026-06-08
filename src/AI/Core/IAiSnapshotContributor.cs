using System.Text.Json.Nodes;
using KitLib.AI.Core.Schema;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.AI.Core;

/// <summary>Mod-provided enrichment for AI snapshots under <c>snapshot["extensions"]</c>.</summary>
public interface IAiSnapshotContributor {
    /// <summary>Unique key under <c>extensions</c> (e.g. <c>lusttravel2</c>, <c>winefox</c>).</summary>
    string ExtensionKey { get; }

    void Enrich(JsonObject snapshot, Player player, GamePhase phase);
}
