using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace KitLib.UI;

/// <summary>Shell root registered with <see cref="ActiveScreenContext" /> for controller default focus.</summary>
public partial class ModPanelShellRoot : Control, IScreenContext {
    private Control? _defaultFocusedControl;

    public Control? DefaultFocusedControl => _defaultFocusedControl;

    public void SetDefaultFocusedControl(Control? control) {
        _defaultFocusedControl = control;
    }

    public override void _EnterTree() {
        ActiveScreenContext.Instance.Update();
    }

    public override void _ExitTree() {
        ActiveScreenContext.Instance.Update();
    }
}
