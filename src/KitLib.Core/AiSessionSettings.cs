using MegaCrit.Sts2.Core.Models;

namespace KitLib;

/// <summary>
/// Per-process AI host / proxy toggles. Not serialized to <c>settings.json</c>;
/// cleared when a run ends so solo or multiplayer runs do not inherit stale enables.
/// </summary>
public static class AiSessionSettings {
    public static bool AutoPlayEnabled { get; set; }
    public static bool MpAiTeammateEnabled { get; set; }
    public static bool MpAiTeammateDriveLiveEnet { get; set; }
    public static bool MpAiTeammateAfkClient { get; set; }
    public static bool SyncBotEnabled { get; set; }
    public static bool SyncBotSpawnPhantomPlayer { get; set; }
    public static CharacterModel? PhantomCharacter { get; set; }
    public static bool PseudoCoopAutoPresetOnLaunch { get; set; }

    public static void ResetRunSession() {
        AutoPlayEnabled = false;
        MpAiTeammateEnabled = false;
        MpAiTeammateDriveLiveEnet = false;
        MpAiTeammateAfkClient = false;
        SyncBotEnabled = false;
        SyncBotSpawnPhantomPlayer = false;
        PhantomCharacter = null;
        PseudoCoopAutoPresetOnLaunch = false;
    }
}
