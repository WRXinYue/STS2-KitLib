namespace KitLib.Settings;

internal static class HotkeyActionId {
    internal const string OpenModPanel = "openModPanel";
    internal const string ToggleRail = "toggleRail";
    internal const string ClosePanel = "closePanel";
    internal const string NextTab = "nextTab";
    internal const string PrevTab = "prevTab";
    internal const string LockRail = "lockRail";
    internal const string QuickSave = "quickSave";
    internal const string QuickLoad = "quickLoad";
    internal const string QuickReplayCombat = "quickReplayCombat";
    internal const string QuickReplayTurn = "quickReplayTurn";
    internal const string TogglePerfHud = "togglePerfHud";

    internal static readonly string[] All = {
        OpenModPanel, ToggleRail, ClosePanel, NextTab, PrevTab, LockRail,
        QuickSave, QuickLoad, QuickReplayCombat, QuickReplayTurn,
        TogglePerfHud
    };
}
