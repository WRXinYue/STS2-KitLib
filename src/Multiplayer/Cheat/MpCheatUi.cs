using System;
using System.Linq;
using DevMode.Settings;
using DevMode.UI;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Multiplayer.Cheat;

/// <summary>Mutable target player selected by host MP picker.</summary>
internal sealed class MpCheatTargetPlayerRef {
    public Player Value { get; set; }
    public MpCheatTargetPlayerRef(Player initial) => Value = initial;
}

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

    /// <summary>Host MP run with 2+ players: player picker row. Returns mutable ref or null.</summary>
    internal static MpCheatTargetPlayerRef? TryBuildTargetPlayerPicker(
        VBoxContainer parent,
        RunState state,
        Player defaultPlayer) {
        if (!MpCheatSession.InMultiplayerRun || !MpCheatSession.IsHost || state.Players.Count <= 1)
            return null;

        var players = state.Players.ToList();
        var playerRow = new HBoxContainer();
        playerRow.AddThemeConstantOverride("separation", 4);
        var playerLbl = new Label { Text = I18N.T("mpcheat.cardAdd.targetPlayer", "Player") };
        playerLbl.AddThemeFontSizeOverride("font_size", 12);
        playerRow.AddChild(playerLbl);
        var playerPicker = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var localNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        var localIdx = 0;
        for (var i = 0; i < players.Count; i++) {
            var p = players[i];
            playerPicker.AddItem(MpCheatPlayerLabels.FormatPickerLabel(p), i);
            if (p.NetId == localNetId)
                localIdx = i;
        }
        playerPicker.Selected = localIdx;
        var targetRef = new MpCheatTargetPlayerRef(players[localIdx]);
        playerPicker.ItemSelected += idx => targetRef.Value = players[(int)idx];
        playerRow.AddChild(playerPicker);
        parent.AddChild(playerRow);
        return targetRef;
    }
}
