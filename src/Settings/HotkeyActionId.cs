namespace KitLib.Settings;

internal static class HotkeyActionId {
    internal const string ToggleRail = "toggleRail";
    internal const string ClosePanel = "closePanel";
    internal const string NextTab = "nextTab";
    internal const string PrevTab = "prevTab";
    internal const string LockRail = "lockRail";
    internal const string QuickSave = "quickSave";
    internal const string QuickLoad = "quickLoad";
    internal const string QuickReplayCombat = "quickReplayCombat";
    internal const string QuickReplayTurn = "quickReplayTurn";

    internal static readonly string[] All = {
        ToggleRail, ClosePanel, NextTab, PrevTab, LockRail,
        QuickSave, QuickLoad, QuickReplayCombat, QuickReplayTurn
    };
}
