namespace KitLib.AI.Core;

/// <summary>Per-character AI capabilities registered with <see cref="CharacterAiRegistry"/>.</summary>
public sealed record CharacterAiProfile(bool SupportsNonCombat = false);
