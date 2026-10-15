using Godot;

namespace KitLib.Integration;

internal static class KitLibHotkeySettingsUi {
    internal const string BindingButtonMeta = "kitlib_hotkey_binding";

    internal static Control BuildSection(bool compact = false) {
        var section = new KitLibHotkeySettingsSection();
        section.Build(compact);
        return section;
    }

    internal static void CancelCapture() => KitLibHotkeySettingsSection.CancelCapture();
}
