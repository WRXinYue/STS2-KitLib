using Godot;
using KitLib.Integration;
using KitLib.ModPanel.Diagnostics;
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
        FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Enabled;
    }

    public override void OnSubmenuOpened() {
        base.OnSubmenuOpened();
        if (!_uiBuilt) {
            ModPanelUI.BuildInto(this);
            _uiBuilt = true;
        }
        if (!_signalsConnected) {
            ConnectSignals();
            _signalsConnected = true;
        }
        ModPanelDiagnostics.LogControllerContext(this);
        Callable.From(() => {
            RefreshControllerFocus();
            RefreshControllerHints();
        }).CallDeferred();
    }

    protected override void OnSubmenuShown() {
        base.OnSubmenuShown();
        Callable.From(RefreshControllerHints).CallDeferred();
    }

    internal void RefreshControllerHints() {
        foreach (var child in GetChildren()) {
            if (child is ModPanelControllerSupport support && GodotObject.IsInstanceValid(support))
                support.RefreshHints();
        }
    }

    public override void OnSubmenuClosed() {
        RitsuModSettingsEmbedHost.FlushDirtyBindings();
        RitsuModSettingsEmbedHost.ClearAfterShellDisposed();
        ModPanelUI.OnSubmenuPopped(this);
        base.OnSubmenuClosed();
    }

    public override void _Input(InputEvent @event) {
        if (!ActiveScreenContext.Instance.IsCurrent(this))
            return;
        foreach (var child in GetChildren()) {
            if (child is not ModPanelControllerSupport support)
                continue;
            if (support.TryHandleDirectionalInput(@event))
                GetViewport()?.SetInputAsHandled();
        }
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
}
