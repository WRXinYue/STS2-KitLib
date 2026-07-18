using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.Singleplayer.Companion;

internal static class SpvCompanionChoiceRouting {
    internal static bool ShouldAutoChoose(Player? player) =>
        player != null
        && SpvCompanionRegistry.IsCompanion(player)
        && SpvCompanionRegistry.IsSingleplayerRun();
}
