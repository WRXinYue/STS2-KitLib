using System.Collections.Generic;
using Godot;

namespace KitLib.Settings;

public sealed class HotkeyBinding {
    public int KeyCode { get; set; }
    public bool Ctrl { get; set; }
    public bool Shift { get; set; }
    public bool Alt { get; set; }

    public Key Keycode => (Key)KeyCode;

    public bool Matches(InputEventKey key) {
        if (!key.Pressed || key.Echo)
            return false;
        return key.Keycode == Keycode
               && key.CtrlPressed == Ctrl
               && key.ShiftPressed == Shift
               && key.AltPressed == Alt;
    }

    public static HotkeyBinding From(InputEventKey key) => new() {
        KeyCode = (int)key.Keycode,
        Ctrl = key.CtrlPressed,
        Shift = key.ShiftPressed,
        Alt = key.AltPressed
    };

    public static HotkeyBinding Of(Key key, bool ctrl = false, bool shift = false, bool alt = false) => new() {
        KeyCode = (int)key,
        Ctrl = ctrl,
        Shift = shift,
        Alt = alt
    };

    public HotkeyBinding Clone() => new() {
        KeyCode = KeyCode,
        Ctrl = Ctrl,
        Shift = Shift,
        Alt = Alt
    };

    public bool EqualsBinding(HotkeyBinding? other) {
        if (other == null)
            return false;
        return KeyCode == other.KeyCode && Ctrl == other.Ctrl && Shift == other.Shift && Alt == other.Alt;
    }

    public string FormatLabel() {
        if (KeyCode == 0)
            return I18N.T("hotkeys.unbound", "Unbound");

        var parts = new List<string>(4);
        if (Ctrl) parts.Add("Ctrl");
        if (Shift) parts.Add("Shift");
        if (Alt) parts.Add("Alt");
        parts.Add(FormatKey(Keycode));
        return string.Join("+", parts);
    }

    private static string FormatKey(Key key) => key switch {
        Key.Escape => "Esc",
        Key.Pageup => "PageUp",
        Key.Pagedown => "PageDown",
        Key.Quoteleft => "`",
        Key.Apostrophe => "'",
        Key.Minus => "-",
        Key.Equal => "=",
        Key.Bracketleft => "[",
        Key.Bracketright => "]",
        Key.Semicolon => ";",
        Key.Comma => ",",
        Key.Period => ".",
        Key.Slash => "/",
        Key.Backslash => "\\",
        Key.Space => "Space",
        Key.Enter => "Enter",
        Key.Tab => "Tab",
        Key.Backspace => "Backspace",
        Key.Delete => "Delete",
        Key.Up => "Up",
        Key.Down => "Down",
        Key.Left => "Left",
        Key.Right => "Right",
        >= Key.F1 and <= Key.F35 => key.ToString(),
        >= Key.Key0 and <= Key.Key9 => ((char)('0' + (key - Key.Key0))).ToString(),
        >= Key.A and <= Key.Z => ((char)('A' + (key - Key.A))).ToString(),
        _ => key.ToString()
    };

    /// <summary>Returns null if valid, otherwise an i18n key for the rejection reason.</summary>
    public static string? ValidateForAssign(string actionId, HotkeyBinding candidate, KitLibSettings settings) {
        if (candidate.KeyCode == 0)
            return "hotkeys.conflict.empty";

        if (IsOfficialDevConsoleKey(candidate))
            return "hotkeys.conflict.devConsole";

        if (UsesGameShortcutKey(candidate.Keycode)) {
            if (actionId != HotkeyActionId.ClosePanel || candidate.Keycode != Key.Escape)
                return "hotkeys.conflict.gameKey";
        }

        foreach (var otherId in HotkeyActionId.All) {
            if (otherId == actionId)
                continue;
            if (candidate.EqualsBinding(settings.GetHotkey(otherId)))
                return "hotkeys.conflict.duplicate";
        }

        return null;
    }

    private static bool HasModifier(HotkeyBinding b) => b.Ctrl || b.Shift || b.Alt;

    /// <summary>
    /// Keys matched by official <c>NInputManager.ProcessShortcutKeyInput</c> (keycode only, no modifiers).
    /// </summary>
    internal static bool UsesGameShortcutKey(Key key) => key switch {
        Key.E or Key.Enter or Key.Space or Key.Escape
            or Key.A or Key.S or Key.D or Key.X or Key.M
            or Key.Up or Key.Down or Key.Left or Key.Right => true,
        >= Key.Key0 and <= Key.Key9 => true,
        _ => false
    };

    private static bool IsOfficialDevConsoleKey(HotkeyBinding b) {
        if (b.Ctrl || b.Alt)
            return false;
        if (b.Shift && b.Keycode == Key.Key8)
            return true;
        return !b.Shift && b.Keycode is Key.Quoteleft or Key.Apostrophe or Key.Asterisk or Key.Asciicircum;
    }
}
