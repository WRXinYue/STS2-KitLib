using KitLib.AI.Core;
using KitLib.AI.Knowledge;
using KitLib.Abstractions.Host;
using KitLib.AI.Planning;
using KitLib.Host;

namespace KitLib.Companion;

/// <summary>Public API for content mods; execution delegates to KitLib.AI when loaded.</summary>
public static class CompanionBridge {
    public static bool IsAvailable =>
        KitLibHost.IsModuleLoaded(KitLibModuleIds.Ai) && !KitLibState.PseudoCoopDeferHeavyUi;

    public static bool IsHostMultiplayerRun =>
        KitLibHost.IsModuleLoaded(KitLibModuleIds.Ai) && KitLibHost.IsHostMultiplayerRun?.Invoke() == true;

    public static IReadOnlyList<CompanionInfo> ListCompanions() =>
        KitLibHost.ListCompanionsHandler?.Invoke() ?? [];

    public static CompanionSpawnResult TrySummon(CompanionSpawnRequest request) {
        if (!KitLibHost.IsModuleLoaded(KitLibModuleIds.Ai))
            return new CompanionSpawnResult(false, 0, "KitLib.AI module is not loaded.");
        return KitLibHost.TrySummonCompanion?.Invoke(request)
            ?? new CompanionSpawnResult(false, 0, "KitLib.AI companion spawn is unavailable.");
    }

    public static bool TryDismiss(ulong netId) {
        if (!KitLibHost.IsModuleLoaded(KitLibModuleIds.Ai))
            return false;
        return KitLibHost.TryDismissCompanion?.Invoke(netId) == true;
    }

    public static void RegisterStrategy(ulong netId, IDecisionMaker strategy) {
        KitLibHost.RegisterNetIdStrategy(netId, strategy);
        KitLibHost.RegisterNetIdStrategyDelegate?.Invoke(netId, strategy);
    }

    public static void UnregisterStrategy(ulong netId) {
        KitLibHost.UnregisterNetIdStrategy(netId);
        KitLibHost.UnregisterNetIdStrategyDelegate?.Invoke(netId);
    }

    public static void RegisterCharacterStrategy(
        string characterId,
        IDecisionMaker strategy,
        CharacterAiProfile? profile = null) =>
        KitLibHost.RegisterCharacterStrategy(characterId, strategy, profile);

    public static void RegisterSnapshotContributor(IAiSnapshotContributor contributor) =>
        KitLibHost.RegisterSnapshotContributor(contributor);

    public static void RegisterMoveModifier(IAiMoveModifier modifier) =>
        KitLibHost.RegisterMoveModifier(modifier);

    public static void RegisterDeckPlanContributor(IDeckPlanContributor contributor) =>
        KitLibHost.RegisterDeckPlanContributor(contributor);

    public static void RegisterCardTagProvider(ICardTagProvider provider) =>
        KitLibHost.RegisterCardTagProvider(provider);

    public static bool TryEnsurePseudoCoopPreset() {
        if (!KitLibHost.IsModuleLoaded(KitLibModuleIds.Ai))
            return false;
        return KitLibHost.TryEnsurePseudoCoopPresetHandler?.Invoke() == true;
    }

    public static void OnRunEnded() {
        KitLibHost.ClearRunState();
        KitLibHost.OnCompanionRunEnded?.Invoke();
    }
}
