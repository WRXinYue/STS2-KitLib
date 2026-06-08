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

    /// <summary>Legacy v3 binding; migrated to <see cref="HotkeyQuickReplayCombat"/> on load.</summary>
    public HotkeyBinding HotkeyQuickRestartTurn { get; set; } = new();

    internal HotkeyBinding GetHotkey(string actionId) => actionId switch {
        HotkeyActionId.ToggleRail => HotkeyToggleRail,
        HotkeyActionId.ClosePanel => HotkeyClosePanel,
        HotkeyActionId.NextTab => HotkeyNextTab,
        HotkeyActionId.PrevTab => HotkeyPrevTab,
        HotkeyActionId.LockRail => HotkeyLockRail,
        HotkeyActionId.QuickSave => HotkeyQuickSave,
        HotkeyActionId.QuickLoad => HotkeyQuickLoad,
        HotkeyActionId.QuickReplayCombat => HotkeyQuickReplayCombat,
        HotkeyActionId.QuickReplayTurn => HotkeyQuickReplayTurn,
        _ => new HotkeyBinding()
    };

    internal void SetHotkey(string actionId, HotkeyBinding binding) {
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
        }
    }
}
