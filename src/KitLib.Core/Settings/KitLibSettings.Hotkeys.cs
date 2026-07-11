namespace KitLib.Settings;

public sealed partial class KitLibSettings {
    public HotkeyBinding HotkeyToggleRail { get; set; } = HotkeyDefaults.ToggleRail.Clone();
    public HotkeyBinding HotkeyClosePanel { get; set; } = HotkeyDefaults.ClosePanel.Clone();
    public HotkeyBinding HotkeyNextTab { get; set; } = HotkeyDefaults.NextTab.Clone();
    public HotkeyBinding HotkeyPrevTab { get; set; } = HotkeyDefaults.PrevTab.Clone();
    public HotkeyBinding HotkeyLockRail { get; set; } = HotkeyDefaults.LockRail.Clone();
    public HotkeyBinding HotkeyQuickSave { get; set; } = HotkeyDefaults.QuickSave.Clone();
    public HotkeyBinding HotkeyQuickLoad { get; set; } = HotkeyDefaults.QuickLoad.Clone();
    public HotkeyBinding HotkeyQuickReplayCombat { get; set; } = HotkeyDefaults.QuickReplayCombat.Clone();
    public HotkeyBinding HotkeyQuickReplayTurn { get; set; } = HotkeyDefaults.QuickReplayTurn.Clone();
    public HotkeyBinding HotkeyTogglePerfHud { get; set; } = HotkeyDefaults.TogglePerfHud.Clone();

    internal HotkeyBinding GetHotkey(string actionId) {
        if (RailTabHotkeyActionId.TryParseTabId(actionId, out var tabId))
            return GetRailTabHotkey(tabId);
        return GetShellHotkey(actionId);
    }

    internal HotkeyBinding GetShellHotkey(string actionId) => actionId switch {
        HotkeyActionId.ToggleRail => HotkeyToggleRail,
        HotkeyActionId.ClosePanel => HotkeyClosePanel,
        HotkeyActionId.NextTab => HotkeyNextTab,
        HotkeyActionId.PrevTab => HotkeyPrevTab,
        HotkeyActionId.LockRail => HotkeyLockRail,
        HotkeyActionId.QuickSave => HotkeyQuickSave,
        HotkeyActionId.QuickLoad => HotkeyQuickLoad,
        HotkeyActionId.QuickReplayCombat => HotkeyQuickReplayCombat,
        HotkeyActionId.QuickReplayTurn => HotkeyQuickReplayTurn,
        HotkeyActionId.TogglePerfHud => HotkeyTogglePerfHud,
        _ => new HotkeyBinding()
    };

    internal void SetHotkey(string actionId, HotkeyBinding binding) {
        if (RailTabHotkeyActionId.TryParseTabId(actionId, out var tabId)) {
            SetRailTabHotkey(tabId, binding);
            return;
        }
        SetShellHotkey(actionId, binding);
    }

    internal void SetShellHotkey(string actionId, HotkeyBinding binding) {
        var copy = binding.Clone();
        switch (actionId) {
            case HotkeyActionId.ToggleRail: HotkeyToggleRail = copy; break;
            case HotkeyActionId.ClosePanel: HotkeyClosePanel = copy; break;
            case HotkeyActionId.NextTab: HotkeyNextTab = copy; break;
            case HotkeyActionId.PrevTab: HotkeyPrevTab = copy; break;
            case HotkeyActionId.LockRail: HotkeyLockRail = copy; break;
            case HotkeyActionId.QuickSave: HotkeyQuickSave = copy; break;
            case HotkeyActionId.QuickLoad: HotkeyQuickLoad = copy; break;
            case HotkeyActionId.QuickReplayCombat: HotkeyQuickReplayCombat = copy; break;
            case HotkeyActionId.QuickReplayTurn: HotkeyQuickReplayTurn = copy; break;
            case HotkeyActionId.TogglePerfHud: HotkeyTogglePerfHud = copy; break;
        }
    }
}
