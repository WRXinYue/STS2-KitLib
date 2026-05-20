using System;
using DevMode.Settings;
using DevMode.UI;
using Godot;

namespace DevMode.Multiplayer.Cheat;

internal static class MpCheatUi {
    internal static bool CanEditCheats =>
        !MpCheatSession.InMultiplayerRun || MpCheatSession.CanEditMultiplayerCheats;

    internal static void AddSessionBanner(VBoxContainer parent) {
        var label = new Label {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);

        if (!MpCheatSession.InMultiplayerRun) {
            if (!SettingsStore.Current.MultiplayerCheatOptIn)
                label.Text = I18N.T(
                    "mpcheat.optIn.hint",
                    "Enable Multiplayer Cheat in Dev Mode menu to sync cheats in co-op (all players need DevMode).");
            parent.AddChild(label);
            return;
        }

        if (MpCheatSession.CanUseMultiplayerCheats) {
            label.Text = MpCheatSession.IsHost
                ? I18N.T("mpcheat.host.active", "Multiplayer cheat: host — changes sync to all players.")
                : I18N.T("mpcheat.client.active", "Multiplayer cheat: client — host controls cheat toggles.");
            label.AddThemeColorOverride("font_color", DevModeTheme.Accent);
        }
        else {
            label.Text = I18N.T(
                "mpcheat.blocked",
                "Multiplayer cheat inactive: {0}",
                MpCheatSession.LastBlockReason ?? "unknown");
        }

        parent.AddChild(label);
    }

    internal static Action<bool> WrapBoolSetter(Action<bool> setter) => v => {
        if (!CanEditCheats) return;
        setter(v);
        AfterCheatChanged();
    };

    internal static Action<float> WrapFloatSetter(Action<float> setter) => v => {
        if (!CanEditCheats) return;
        setter(v);
        AfterCheatChanged();
    };

    internal static void AfterCheatChanged() {
        if (MpCheatSession.InMultiplayerRun && MpCheatSession.IsHost)
            MpCheatSync.HostPublishFromDevModeState("ui");
    }

    internal static bool IsFrameCheatAllowed => MpCheatApplier.FrameCheatsAllowed;
}
