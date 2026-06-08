namespace KitLib.AI.Core;

public static class CharacterAiRegistry {
    public static void Register(string characterId, IDecisionMaker strategy, CharacterAiProfile? profile = null) =>
        KitLib.Host.KitLibHost.RegisterCharacterStrategy(characterId, strategy, profile);

    public static void Unregister(string characterId) =>
        KitLib.Host.KitLibHost.UnregisterCharacterStrategy(characterId);

    public static bool TryGet(string? characterId, out IDecisionMaker strategy) =>
        KitLib.Host.KitLibHost.TryGetCharacterStrategy(characterId, out strategy!);

    public static bool TryGetProfile(string? characterId, out CharacterAiProfile profile) =>
        KitLib.Host.KitLibHost.TryGetCharacterProfile(characterId, out profile);

    public static bool SupportsNonCombat(string? characterId) =>
        TryGetProfile(characterId, out var profile) && profile.SupportsNonCombat;

    internal static void ClearOnRunEnd() { }
}
