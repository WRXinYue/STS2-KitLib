namespace KitLib.AI.Core;

public static class CharacterAiRegistry {
    public static void Register(string characterId, IDecisionMaker strategy, CharacterAiProfile? profile = null) =>
        KitLib.Host.KitLibHost.RegisterCharacterStrategy(characterId, strategy, profile);

    public static void Unregister(string characterId) =>
        KitLib.Host.KitLibHost.UnregisterCharacterStrategy(characterId);

    public static bool TryGet(string? characterId, out IDecisionMaker strategy) {
        if (!KitLib.Host.KitLibHost.TryGetCharacterStrategy(characterId, out var raw) || raw is not IDecisionMaker maker) {
            strategy = null!;
            return false;
        }

        strategy = maker;
        return true;
    }

    public static bool TryGetProfile(string? characterId, out CharacterAiProfile profile) {
        if (!KitLib.Host.KitLibHost.TryGetCharacterProfile(characterId, out var raw) || raw is not CharacterAiProfile resolved) {
            profile = default;
            return false;
        }

        profile = resolved;
        return true;
    }

    public static bool SupportsNonCombat(string? characterId) =>
        TryGetProfile(characterId, out var profile) && profile.SupportsNonCombat;

    internal static void ClearOnRunEnd() { }
}
