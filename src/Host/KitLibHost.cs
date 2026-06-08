using System.Collections.Generic;
using System.Text.Json.Nodes;
using KitLib.AI.Core;
using KitLib.AI.Core.Schema;
using KitLib.AI.Knowledge;
using KitLib.Abstractions.Host;
using KitLib.AI.Planning;
using KitLib.Companion;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.Host;

/// <summary>Core registration platform for KitLib satellite mods and content-mod bridges.</summary>
public static class KitLibHost {
    static readonly List<KitLibTabDescriptor> Tabs = [];
    static readonly List<IAiSnapshotContributor> SnapshotContributors = [];
    static readonly List<IAiMoveModifier> MoveModifiers = [];
    static readonly List<ICardTagProvider> CardTagProviders = [];
    static readonly Dictionary<string, IDecisionMaker> CharacterStrategies = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, CharacterAiProfile> CharacterProfiles = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<ulong, IDecisionMaker> NetIdStrategies = [];
    static bool _bootstrapped;

    public static void Bootstrap() {
        if (_bootstrapped) return;
        _bootstrapped = true;
        ModuleCatalog.Announce(KitLibModuleIds.Core);
        MainFile.Logger.Info("KitLib Core host ready.");
    }

    public static void AnnounceModule(string moduleId) => ModuleCatalog.Announce(moduleId);

    public static void RegisterModule(IKitLibModule module) => ModuleCatalog.Register(module);

    public static bool IsModuleLoaded(string moduleId) => ModuleCatalog.IsLoaded(moduleId);

    public static void RegisterTab(KitLibTabDescriptor tab) {
        ArgumentNullException.ThrowIfNull(tab);
        Tabs.RemoveAll(t => t.Id == tab.Id);
        Tabs.Add(tab);
    }

    public static IReadOnlyList<KitLibTabDescriptor> GetTabs(KitLibTabGroup group) {
        var list = new List<KitLibTabDescriptor>();
        foreach (var tab in Tabs) {
            if (tab.Group == group)
                list.Add(tab);
        }
        list.Sort((a, b) => a.Order.CompareTo(b.Order));
        return list;
    }

    public static IReadOnlyList<KitLibTabDescriptor> GetAllTabs() => Tabs.AsReadOnly();

    public static void RegisterSnapshotContributor(IAiSnapshotContributor contributor) {
        SnapshotContributors.Add(contributor);
        MainFile.Logger.Info($"[KitLibHost] Snapshot contributor key={contributor.ExtensionKey}.");
    }

    public static void EnrichSnapshot(JsonObject snapshot, Player player, GamePhase phase) {
        if (SnapshotContributors.Count == 0) return;
        snapshot["extensions"] ??= new JsonObject();
        foreach (var contributor in SnapshotContributors) {
            try {
                contributor.Enrich(snapshot, player, phase);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[KitLibHost] Snapshot {contributor.ExtensionKey} failed: {ex.Message}");
            }
        }
    }

    public static void RegisterMoveModifier(IAiMoveModifier modifier) {
        MoveModifiers.Add(modifier);
        MainFile.Logger.Info($"[KitLibHost] Move modifier registered type={modifier.GetType().Name}.");
    }

    public static int ApplyMoveModifiers(JsonObject snapshot, GameAction move, int baseScore, string? characterId) {
        var score = baseScore;
        foreach (var modifier in MoveModifiers) {
            if (!modifier.AppliesTo(characterId)) continue;
            try {
                score = modifier.ModifyScore(snapshot, move, score);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[KitLibHost] Move modifier {modifier.GetType().Name} failed: {ex.Message}");
            }
        }
        return score;
    }

    public static void RegisterCardTagProvider(ICardTagProvider provider) {
        CardTagProviders.Add(provider);
        MainFile.Logger.Info($"[KitLibHost] Card tag provider registered type={provider.GetType().Name}.");
    }

    public static IReadOnlyList<AiTag> MergeCardTags(string? cardId, IReadOnlyList<AiTag> baseTags) {
        if (string.IsNullOrWhiteSpace(cardId) || CardTagProviders.Count == 0)
            return baseTags;
        var merged = new HashSet<AiTag>(baseTags);
        foreach (var provider in CardTagProviders) {
            if (!provider.AppliesTo(cardId)) continue;
            try {
                foreach (var tag in provider.GetExtraTags(cardId))
                    merged.Add(tag);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[KitLibHost] Card tag provider {provider.GetType().Name} failed: {ex.Message}");
            }
        }
        return merged.ToList();
    }

    public static void RegisterCharacterStrategy(string characterId, IDecisionMaker strategy, CharacterAiProfile? profile = null) {
        CharacterStrategies[characterId] = strategy;
        if (profile != null)
            CharacterProfiles[characterId] = profile;
        MainFile.Logger.Info($"[KitLibHost] Character strategy registered id={characterId}.");
    }

    public static void UnregisterCharacterStrategy(string characterId) {
        CharacterStrategies.Remove(characterId);
        CharacterProfiles.Remove(characterId);
    }

    public static bool TryGetCharacterStrategy(string? characterId, out IDecisionMaker strategy) {
        strategy = null!;
        return !string.IsNullOrEmpty(characterId) && CharacterStrategies.TryGetValue(characterId, out strategy!);
    }

    public static bool TryGetCharacterProfile(string? characterId, out CharacterAiProfile profile) {
        profile = default;
        return !string.IsNullOrEmpty(characterId) && CharacterProfiles.TryGetValue(characterId, out profile);
    }

    public static void RegisterNetIdStrategy(ulong netId, IDecisionMaker strategy) {
        if (netId == 0) return;
        NetIdStrategies[netId] = strategy;
    }

    public static void UnregisterNetIdStrategy(ulong netId) {
        if (netId == 0) return;
        NetIdStrategies.Remove(netId);
    }

    public static bool TryGetNetIdStrategy(ulong netId, out IDecisionMaker strategy) =>
        NetIdStrategies.TryGetValue(netId, out strategy!);

    public static void ClearRunState() {
        NetIdStrategies.Clear();
    }

    public static Func<CompanionSpawnRequest, CompanionSpawnResult>? TrySummonCompanion { get; set; }
    public static Func<ulong, bool>? TryDismissCompanion { get; set; }
    public static Func<IReadOnlyList<CompanionInfo>>? ListCompanionsHandler { get; set; }
    public static Func<bool>? TryEnsurePseudoCoopPresetHandler { get; set; }
    public static Func<bool>? IsHostMultiplayerRun { get; set; }
    public static Action<ulong, IDecisionMaker>? RegisterNetIdStrategyDelegate { get; set; }
    public static Action<ulong>? UnregisterNetIdStrategyDelegate { get; set; }
    public static Action<IDeckPlanContributor>? RegisterDeckPlanContributorHandler { get; set; }
    public static Action? StopAiPlayLoop { get; set; }
    public static Action? OnCompanionRunEnded { get; set; }
    public static Func<bool>? IsDualInstanceActive { get; set; }

    public static void RegisterDeckPlanContributor(IDeckPlanContributor contributor) =>
        RegisterDeckPlanContributorHandler?.Invoke(contributor);
}
