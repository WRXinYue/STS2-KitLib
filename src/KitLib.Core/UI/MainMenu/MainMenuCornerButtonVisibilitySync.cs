using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

internal sealed partial class MainMenuCornerButtonVisibilitySync : Node {
    NMainMenu? _mainMenu;
    Control? _host;
    float _lastSlotTop = float.NaN;
    float _lastOffsetRight = float.NaN;

    public void Configure(NMainMenu mainMenu, Control host) {
        _mainMenu = mainMenu;
        _host = host;
        _lastSlotTop = float.NaN;
        _lastOffsetRight = float.NaN;
    }

    public override void _Process(double delta) {
        if (_mainMenu == null ||
            _host == null ||
            !IsInstanceValid(_mainMenu) ||
            !IsInstanceValid(_host)) {
            QueueFree();
            return;
        }

        if (_mainMenu.GetNodeOrNull<Control>("%PatchNotesButton") is { } patchNotesButton &&
            IsInstanceValid(patchNotesButton)) {
            MainMenuCornerButtonHost.SyncVisibility(_mainMenu);
            MainMenuCornerButtonHost.TrySyncPlacementIfLayoutChanged(
                _mainMenu, _host, patchNotesButton, ref _lastSlotTop, ref _lastOffsetRight);
            return;
        }

        MainMenuCornerButtonHost.SyncVisibility(_mainMenu);
    }
}
