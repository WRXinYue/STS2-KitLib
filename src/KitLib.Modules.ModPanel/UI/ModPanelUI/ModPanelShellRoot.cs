using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace KitLib.UI;

/// <summary>Shell root registered with <see cref="ActiveScreenContext" /> for controller default focus.</summary>
public partial class ModPanelShellRoot : Control, IScreenContext {
    private Control? _defaultFocusedControl;

    public Control? DefaultFocusedControl => _defaultFocusedControl;

    public void SetDefaultFocusedControl(Control? control) {
        _defaultFocusedControl = control;
    }

    public override void _Ready() {
        SetProcessInput(true);
        FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Enabled;
    }

    public override void _EnterTree() {
        ActiveScreenContext.Instance.Update();
        Callable.From(RefreshScreenContextFocus).CallDeferred();
    }

    public override void _ExitTree() {
        FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Disabled;
        ActiveScreenContext.Instance.Update();
    }

    public override void _Input(InputEvent @event) {
        foreach (var child in GetChildren()) {
            if (child is not ModPanelControllerSupport support)
                continue;
            if (support.TryHandleDirectionalInput(@event)) {
                GetViewport()?.SetInputAsHandled();
                return;
            }
        }
    }

    internal void RefreshScreenContextFocus() {
        if (!GodotObject.IsInstanceValid(this))
            return;
        this.UpdateControllerNavEnabled();
        if (NControllerManager.Instance?.IsUsingController != true)
            return;
        GetViewport()?.GuiReleaseFocus();
        ActiveScreenContext.Instance.FocusOnDefaultControl();
    }
}
