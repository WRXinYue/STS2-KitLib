using Godot;
using KitLib.Abstractions.Host;
using KitLib.Host;
using KitLib.Integration;
using KitLib.Modding;
using KitLib.ModPanel.Diagnostics;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace KitLib.UI;

/// <summary>Official <see cref="NSubmenu" /> stack entry for KitLib mod settings (same pattern as RitsuLib).</summary>
public partial class ModPanelSubmenu : NSubmenu {
    private bool _uiBuilt;
    private bool _signalsConnected;
    private Control? _initialFocusedControl;

    protected override Control? InitialFocusedControl => _initialFocusedControl;

    internal void SetInitialFocusedControl(Control? control) => _initialFocusedControl = control;

    public override void _Ready() {
        Name = "KitLibModPanelSubmenu";
        ZIndex = 2000;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Both;
        MouseFilter = MouseFilterEnum.Stop;
        SetProcessInput(true);
        SetProcessUnhandledInput(true);
        FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Enabled;
    }

    private void ForEachControllerSupport(Action<ModPanelControllerSupport> action) {
        foreach (var child in GetChildren()) {
            if (child is ModPanelControllerSupport support && GodotObject.IsInstanceValid(support))
                action(support);
        }
    }

    private void EnableTabHotkeys() => ForEachControllerSupport(s => s.EnableTabHotkeys());

    private void DisableTabHotkeys() => ForEachControllerSupport(s => s.DisableTabHotkeys());

    public override void OnSubmenuOpened() {
        base.OnSubmenuOpened();
        ThemeManager.OnThemeChanged += OnKitLibThemeChanged;
        RitsuModSettingsEmbedHost.Ensure();
        if (!_uiBuilt) {
            ModPanelUI.BuildInto(this);
            _uiBuilt = true;
        }
        if (!_signalsConnected) {
            ConnectSignals();
            _signalsConnected = true;
        }
        ModPanelDiagnostics.LogControllerContext(this);
        EnableTabHotkeys();
        Callable.From(() => {
            RefreshControllerFocus();
            RefreshControllerHints();
        }).CallDeferred();
    }

    protected override void OnSubmenuShown() {
        base.OnSubmenuShown();
        EnableTabHotkeys();
        Callable.From(RefreshControllerHints).CallDeferred();
    }

    protected override void OnSubmenuHidden() {
        DisableTabHotkeys();
        base.OnSubmenuHidden();
    }

    internal void RefreshControllerHints() {
        foreach (var child in GetChildren()) {
            if (child is ModPanelControllerSupport support && GodotObject.IsInstanceValid(support))
                support.RefreshHints();
        }
    }

    public override void OnSubmenuClosed() {
        DisableTabHotkeys();
        ThemeManager.OnThemeChanged -= OnKitLibThemeChanged;
        KitLibHotkeySettingsUi.CancelCapture();
        if (KitLibHost.IsModuleLoaded(KitLibModuleIds.User))
            ModRuntime.LoadSettings.Persist();
        RitsuModSettingsEmbedHost.FlushDirtyBindings();
        ModPanelUI.OnSubmenuPopped(this);
        base.OnSubmenuClosed();
    }

    public override void _Input(InputEvent @event) {
        if (!ActiveScreenContext.Instance.IsCurrent(this))
            return;
        if (TryHandleControllerInput(@event)) {
            GetViewport()?.SetInputAsHandled();
            return;
        }
    }

    public override void _UnhandledInput(InputEvent @event) {
        if (!ActiveScreenContext.Instance.IsCurrent(this))
            return;
        if (TryHandleControllerInput(@event)) {
            GetViewport()?.SetInputAsHandled();
            return;
        }
    }

    private bool TryHandleControllerInput(InputEvent @event) {
        foreach (var child in GetChildren()) {
            if (child is not ModPanelControllerSupport support)
                continue;
            if (support.TryHandleTabInput(@event))
                return true;
            if (support.TryHandleDirectionalInput(@event))
                return true;
        }
        return false;
    }

    internal void RefreshControllerFocus() {
        if (!GodotObject.IsInstanceValid(this))
            return;
        this.UpdateControllerNavEnabled();
        if (NControllerManager.Instance?.IsUsingController != true)
            return;
        GetViewport()?.GuiReleaseFocus();
        ActiveScreenContext.Instance.FocusOnDefaultControl();
    }

    private static void OnKitLibThemeChanged() => ModPanelUI.HandleThemeChanged();
}
