using KitLib.Replay.Commands;

namespace KitLib.Replay;

/// <summary>
/// Live pacing matches a real player's tempo. Off = the game's raw speed.
/// </summary>
internal static class ReplayLiveMode {
    public static bool Enabled { get; set; } = true;

    public static bool ConsumeThink(ReplayCommand cmd, out int delayMs) {
        delayMs = 0;
        if (cmd.HasThought)
            return false;
        cmd.HasThought = true;
        if (!Enabled || ReplayDispatcher.IsSeeking)
            return false;
        delayMs = ThinkMs(cmd);
        return delayMs > 0;
    }

    public static int ThinkMs(ReplayCommand cmd) => cmd switch {
        PlayCardCommand => 850,
        UsePotionCommand => 700,
        DiscardPotionCommand => 400,
        ChooseEventOptionCommand opt => opt.RecordedIndex < 0 ? 350 : 1000,
        ChooseRelicCommand => 900,
        SelectCardFromScreenCommand => 750,
        SelectGridCardCommand => 600,
        ClickGridCardCommand => 400,
        MapMoveCommand => 450,
        BuyCardCommand or BuyRelicCommand or BuyPotionCommand or BuyCardRemovalCommand => 550,
        ChooseRestSiteOptionCommand => 700,
        ClaimRewardCommand or TakeCardCommand => 500,
        EndTurnCommand => 400,
        CrystalSphereClickCommand => 500,
        _ => 0,
    };
}
