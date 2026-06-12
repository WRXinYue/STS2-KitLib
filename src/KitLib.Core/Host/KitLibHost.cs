using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.Modding;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.Host;

/// <summary>Core registration platform for KitLib satellite mods and content-mod bridges.</summary>
public static class KitLibHost {
    static readonly List<object> Tabs = [];
    static readonly List<object> SnapshotContributors = [];
    static readonly List<object> MoveModifiers = [];
    static readonly List<object> CardTagProviders = [];
    static readonly Dictionary<string, object> CharacterStrategies = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, object?> CharacterProfiles = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<ulong, object> NetIdStrategies = [];
    static bool _bootstrapped;

    /// <summary>Pinned mod_data root; propagated to satellite assemblies via delegates.</summary>
    public static string ModDataDir { get; private set; } = "";

    internal static void PinModDataDir(string path) => ModDataDir = path;

    internal static Func<object>? CatalogAccessor;

    public static void Bootstrap() {
        if (_bootstrapped) return;
        _bootstrapped = true;
        KitLog.Info("KitLib host bootstrap starting.");
        ModuleCatalog.Announce(ModuleIds.Core);
        KitLog.Info("KitLib core module announced; loading bundled satellites.");
        SatelliteModuleLoader.LoadBundledModules();
        if (!ModuleCatalog.IsLoaded(ModuleIds.Panel))
            KitLog.Warn("Hotkeys require KitLib.Panel — module not loaded (check Settings → satellite profile).");
        KitLog.Info("KitLib Core host ready.");
    }

    public static void AnnounceModule(string moduleId) => ModuleCatalog.Announce(moduleId);

    public static void RegisterModule(object module) => ModuleCatalog.Register(module);

    public static bool IsModuleLoaded(string moduleId) => ModuleCatalog.IsLoaded(moduleId);

    /// <summary>Read-only loaded-mod catalog (cast to <c>KitLib.Abstractions.Modding.IModCatalog</c>).</summary>
    public static object ModCatalog =>
        CatalogAccessor?.Invoke() ?? throw new InvalidOperationException("KitLib.User is not loaded.");

    /// <summary>Mod settings panel host; null when <c>KitLib.ModPanel</c> is not loaded.</summary>
    public static object? ModSettings { get; private set; }

    public static void RegisterModSettingsPanelHost(object host) {
        ArgumentNullException.ThrowIfNull(host);
        ModSettings = host;
    }

    public static void RegisterTab(object tab) {
        ArgumentNullException.ThrowIfNull(tab);
        var id = HostReflection.GetStringProperty(tab, "Id");
        if (!string.IsNullOrEmpty(id))
            Tabs.RemoveAll(t => string.Equals(HostReflection.GetStringProperty(t, "Id"), id, StringComparison.Ordinal));
        Tabs.Add(tab);
    }

    /// <param name="tabGroup"><c>KitLib.Abstractions.Host.KitLibTabGroup</c> ordinal.</param>
    public static IReadOnlyList<object> GetTabs(int tabGroup) {
        var list = new List<object>();
        foreach (var tab in Tabs) {
            if (HostReflection.GetIntProperty(tab, "Group") == tabGroup)
                list.Add(tab);
        }
        list.Sort((a, b) => HostReflection.GetIntProperty(a, "Order").CompareTo(HostReflection.GetIntProperty(b, "Order")));
        return list;
    }

    public static IReadOnlyList<object> GetAllTabs() => Tabs.AsReadOnly();

    public static void RegisterSnapshotContributor(object contributor) {
        SnapshotContributors.Add(contributor);
        var key = HostReflection.GetStringProperty(contributor, "ExtensionKey") ?? contributor.GetType().Name;
        KitLog.Info("Host", $"Snapshot contributor key={key}.");
    }

    public static void EnrichSnapshot(JsonObject snapshot, Player player, object gamePhase) {
        if (SnapshotContributors.Count == 0) return;
        snapshot["extensions"] ??= new JsonObject();
        foreach (var contributor in SnapshotContributors) {
            try {
                HostReflection.InvokeEnrich(contributor, snapshot, player, gamePhase);
            }
            catch (Exception ex) {
                KitLog.Warn("Host", $"Snapshot contributor failed: {ex.Message}");
            }
        }
    }

    public static void RegisterMoveModifier(object modifier) {
        MoveModifiers.Add(modifier);
        KitLog.Info("Host", $"Move modifier registered type={modifier.GetType().Name}.");
    }

    public static int ApplyMoveModifiers(JsonObject snapshot, object move, int baseScore, string? characterId) {
        var score = baseScore;
        foreach (var modifier in MoveModifiers) {
            if (!HostReflection.InvokeAppliesTo(modifier, characterId)) continue;
            try {
                score = HostReflection.InvokeModifyScore(modifier, snapshot, move, score);
            }
            catch (Exception ex) {
                KitLog.Warn("Host", $"Move modifier {modifier.GetType().Name} failed: {ex.Message}");
            }
        }
        return score;
    }

    public static void RegisterCardTagProvider(object provider) {
        CardTagProviders.Add(provider);
        KitLog.Info("Host", $"Card tag provider registered type={provider.GetType().Name}.");
    }

    public static IReadOnlyList<object> MergeCardTags(string? cardId, IReadOnlyList<object> baseTags) {
        if (string.IsNullOrWhiteSpace(cardId) || CardTagProviders.Count == 0)
            return baseTags;
        var merged = new HashSet<object>(baseTags);
        foreach (var provider in CardTagProviders) {
            if (!HostReflection.InvokeAppliesTo(provider, cardId)) continue;
            try {
                foreach (var tag in HostReflection.InvokeGetExtraTags(provider, cardId))
                    merged.Add(tag);
            }
            catch (Exception ex) {
                KitLog.Warn("Host", $"Card tag provider {provider.GetType().Name} failed: {ex.Message}");
            }
        }
        return merged.ToList();
    }

    public static void RegisterCharacterStrategy(string characterId, object strategy, object? profile = null) {
        CharacterStrategies[characterId] = strategy;
        if (profile != null)
            CharacterProfiles[characterId] = profile;
        KitLog.Info("Host", $"Character strategy registered id={characterId}.");
    }

    public static void UnregisterCharacterStrategy(string characterId) {
        CharacterStrategies.Remove(characterId);
        CharacterProfiles.Remove(characterId);
    }

    public static bool TryGetCharacterStrategy(string? characterId, out object strategy) {
        strategy = null!;
        return !string.IsNullOrEmpty(characterId) && CharacterStrategies.TryGetValue(characterId, out strategy!);
    }

    public static bool TryGetCharacterProfile(string? characterId, out object? profile) {
        profile = default;
        return !string.IsNullOrEmpty(characterId) && CharacterProfiles.TryGetValue(characterId, out profile);
    }

    public static void RegisterNetIdStrategy(ulong netId, object strategy) {
        if (netId == 0) return;
        NetIdStrategies[netId] = strategy;
    }

    public static void UnregisterNetIdStrategy(ulong netId) {
        if (netId == 0) return;
        NetIdStrategies.Remove(netId);
    }

    public static bool TryGetNetIdStrategy(ulong netId, out object strategy) =>
        NetIdStrategies.TryGetValue(netId, out strategy!);

    public static void ClearRunState() {
        NetIdStrategies.Clear();
    }

    public static Func<Companion.CompanionSpawnRequest, Companion.CompanionSpawnResult>? TrySummonCompanion { get; set; }
    public static Func<ulong, bool>? TryDismissCompanion { get; set; }
    public static Func<IReadOnlyList<Companion.CompanionInfo>>? ListCompanionsHandler { get; set; }
    public static Func<bool>? TryEnsurePseudoCoopPresetHandler { get; set; }
    public static Func<bool>? IsHostMultiplayerRun { get; set; }
    public static Action<ulong, object>? RegisterNetIdStrategyDelegate { get; set; }
    public static Action<ulong>? UnregisterNetIdStrategyDelegate { get; set; }
    public static Action<object>? RegisterDeckPlanContributorHandler { get; set; }
    public static Action? StopAiPlayLoop { get; set; }
    public static Action? OnCompanionRunEnded { get; set; }
    public static Func<bool>? IsDualInstanceActive { get; set; }
    public static Action? SyncAiHudOverlay { get; set; }
    public static Action? SyncPerfHudOverlay { get; set; }
    public static Action? NotifyPerfHudEnabledChanged { get; set; }
    public static Action? NotifyHotkeySettingsChanged { get; set; }
    public static Func<Creature, IReadOnlyList<Creature>, object, JsonArray>? CaptureMonsterIntentSteps { get; set; }

    /// <summary>CombatState is passed as object to avoid cross-ALC MissingMethod on Panel→Dev calls.</summary>
    public static Func<object?, bool>? IsMonsterIntentOverlayReady { get; set; }

    public static Func<object?, object?>? CaptureMonsterIntentCurrent { get; set; }

    public static Func<object?, object?>? CaptureMonsterIntentNextTurn { get; set; }

    /// <summary>Registered by <c>KitLib.Dev.ModuleEntry</c>; invoked from <c>KitLibProcessNode</c> when SceneTree is ready.</summary>
    public static Action? RequestDevBootstrap { get; set; }

    /// <summary>Registered by <c>KitLib.Dev.ModuleEntry</c>; Dev Harmony applied after satellite load returns.</summary>
    public static Action? EnsureDevHarmonyApplied { get; set; }

    public static void TryEnsureDevHarmonyApplied() {
        try {
            EnsureDevHarmonyApplied?.Invoke();
        }
        catch (Exception ex) {
            KitLog.Warn($"Dev Harmony apply failed: {ex.Message}");
        }
    }

    public static void TryRunDevBootstrap() {
        KitLibBootstrapGate.EnterSceneReadyBootstrap();
        try {
            RequestDevBootstrap?.Invoke();
        }
        catch (Exception ex) {
            BootstrapDiagnostics.RecordFailure("TryRunDevBootstrap", ex);
        }
    }

    public static void RegisterDeckPlanContributor(object contributor) =>
        RegisterDeckPlanContributorHandler?.Invoke(contributor);
}
