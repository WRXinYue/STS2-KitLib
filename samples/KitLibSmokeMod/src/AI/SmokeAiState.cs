using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLibSmokeMod.AI;

internal static class SmokeAiState {
    internal const string CharacterId = "SMOKE_TEST_CHARACTER";
    internal const string ExtensionKey = "kitlib_smoke";

    internal static bool IsTarget(Player player) =>
        string.Equals(player.Character?.Id.Entry, CharacterId, StringComparison.OrdinalIgnoreCase);
}
