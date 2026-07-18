extern alias KitLibCore;

using KitLib.Abstractions.Modding;
using KitLib.AI.Core;
using KitLibCore::KitLib.Companion;
using KitLibSmokeMod.AI;

namespace KitLibSmokeMod;

/// <summary>KitLib content-mod integration used by <see cref="MainFile"/> and CI load tests.</summary>
internal static class KitLibSmokeRegistration {
    public static void Register() {
        if (!CompanionBridge.IsAvailable)
            return;

        CompanionBridge.RegisterCharacterProfile(
            SmokeAiState.CharacterId,
            new CharacterAiProfile(SupportsNonCombat: true));

        CompanionBridge.RegisterSnapshotContributor(SmokeSnapshotContributor.Instance);
        CompanionBridge.RegisterMoveModifier(SmokeMoveModifier.Instance);

        KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
            ModId = "KitLibSmokeMod",
            PageId = "smoke",
            Title = "Smoke",
            SortOrder = 0,
            BuildBody = static () => new object(),
        });
    }
}
