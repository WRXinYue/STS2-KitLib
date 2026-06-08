using System;
using System.Collections.Generic;

namespace KitLib.AI.Core;

/// <summary>Maps character model ids to default <see cref="IDecisionMaker"/> implementations (mod-registered).</summary>
public static class CharacterAiRegistry {
    static readonly Dictionary<string, IDecisionMaker> Strategies = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, CharacterAiProfile> Profiles = new(StringComparer.OrdinalIgnoreCase);

    public static void Register(string characterId, IDecisionMaker strategy, CharacterAiProfile? profile = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(characterId);
        ArgumentNullException.ThrowIfNull(strategy);

        var key = NormalizeId(characterId);
        Strategies[key] = strategy;
        Profiles[key] = profile ?? new CharacterAiProfile();
        MainFile.Logger.Info($"[AiRegistry] Character strategy registered id={key}.");
    }

    public static void Unregister(string characterId) {
        var key = NormalizeId(characterId);
        Strategies.Remove(key);
        Profiles.Remove(key);
    }

    public static bool TryGet(string? characterId, out IDecisionMaker strategy) {
        strategy = null!;
        if (string.IsNullOrWhiteSpace(characterId))
            return false;
        return Strategies.TryGetValue(NormalizeId(characterId), out strategy!);
    }

    public static bool TryGetProfile(string? characterId, out CharacterAiProfile profile) {
        profile = new CharacterAiProfile();
        if (string.IsNullOrWhiteSpace(characterId))
            return false;
        return Profiles.TryGetValue(NormalizeId(characterId), out profile!);
    }

    public static bool SupportsNonCombat(string? characterId) =>
        TryGetProfile(characterId, out var profile) && profile.SupportsNonCombat;

    internal static void ClearOnRunEnd() {
        // Mod registrations persist across runs; only per-netId overrides are cleared elsewhere.
    }

    static string NormalizeId(string characterId) => characterId.Trim();
}
