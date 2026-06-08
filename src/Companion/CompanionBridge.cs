using System.Collections.Generic;
using System.Linq;
using KitLib.AI.Core;
using KitLib.AI.Knowledge;
using KitLib.AI.Planning;
using KitLib.Multiplayer.Cheat;
using KitLib.Multiplayer.PseudoCoop;
using KitLib.Multiplayer.SyncBot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Companion;

/// <summary>Public API for spawning AI companions in pseudo-coop / host multiplayer runs.</summary>
public static class CompanionBridge {
    public static bool IsAvailable =>
        !KitLibState.PseudoCoopDeferHeavyUi;

    public static bool IsHostMultiplayerRun =>
        MpCheatSession.InMultiplayerRun && MpCheatSession.IsHost;

    public static IReadOnlyList<CompanionInfo> ListCompanions() {
        var state = RunManager.Instance?.DebugOnlyGetState();
        var hostNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        if (state == null || hostNetId == 0)
            return [];

        return state.Players
            .Where(p => p.NetId != hostNetId)
            .Select(p => new CompanionInfo(
                p.NetId,
                p.Character!.Id,
                SimulatedPeerRegistry.IsHostDrivenPeer(p.NetId),
                p.Creature.IsAlive))
            .ToList();
    }

    public static CompanionSpawnResult TrySummon(CompanionSpawnRequest request) =>
        CompanionSpawnService.TrySpawn(request);

    /// <summary>Stops AI control and unregisters lobby roster entry; does not remove the player from run state.</summary>
    public static bool TryDismiss(ulong netId) {
        if (netId == 0) return false;

        var hostNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        if (netId == hostNetId) return false;

        CompanionRegistry.Unregister(netId);
        PseudoCoopLobbyRoster.UnregisterSimulatedPeer(netId);
        SimulatedPeerRegistry.Refresh();
        MainFile.Logger.Info($"[Companion] Dismissed netId={netId} (AI/roster only).");
        return true;
    }

    public static void RegisterStrategy(ulong netId, IDecisionMaker strategy) =>
        CompanionRegistry.Register(netId, strategy);

    public static void UnregisterStrategy(ulong netId) =>
        CompanionRegistry.Unregister(netId);

    /// <summary>Register a default strategy for all players of this character (mod init).</summary>
    public static void RegisterCharacterStrategy(
        string characterId,
        IDecisionMaker strategy,
        CharacterAiProfile? profile = null) =>
        CharacterAiRegistry.Register(characterId, strategy, profile);

    public static void RegisterSnapshotContributor(IAiSnapshotContributor contributor) =>
        AiSnapshotHub.Register(contributor);

    public static void RegisterMoveModifier(IAiMoveModifier modifier) =>
        AiMoveModifierHub.Register(modifier);

    public static void RegisterDeckPlanContributor(IDeckPlanContributor contributor) =>
        DeckPlanContributorHub.Register(contributor);

    public static void RegisterCardTagProvider(ICardTagProvider provider) =>
        CardTagProviderHub.Register(provider);

    public static bool TryEnsurePseudoCoopPreset() {
        if (!IsAvailable) return false;
        PseudoCoopBootstrap.ApplyPreset();
        return true;
    }

    internal static void OnRunEnded() {
        CompanionRegistry.ClearOnRunEnd();
        CompanionNonCombatRegistry.ClearOnRunEnd();
    }
}
