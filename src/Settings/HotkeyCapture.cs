using System;
using Godot;

namespace KitLib.Settings;

/// <summary>Captures the next key press for hotkey rebinding in Settings.</summary>
internal static class HotkeyCapture {
    private static string? _listeningActionId;
    private static Action<string?, HotkeyBinding?>? _callback;

    internal static bool IsListening => _listeningActionId != null;

    internal static string? ListeningActionId => _listeningActionId;

    internal static void Begin(string actionId, Action<string?, HotkeyBinding?> onComplete) {
        Cancel();
        _listeningActionId = actionId;
        _callback = onComplete;
    }

    internal static void Cancel() {
        _listeningActionId = null;
        _callback = null;
    }

    /// <summary>Returns true if the event was consumed by capture.</summary>
    internal static bool TryCapture(InputEvent @event, Viewport viewport) {
        if (_listeningActionId == null || _callback == null)
            return false;
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return false;

        var actionId = _listeningActionId;
        var callback = _callback;
        Cancel();

        if (key.Keycode == Key.Escape) {
            callback(actionId, null);
            viewport.SetInputAsHandled();
            return true;
        }

        var binding = HotkeyBinding.From(key);
        callback(actionId, binding);
        viewport.SetInputAsHandled();
        return true;
    }
}
