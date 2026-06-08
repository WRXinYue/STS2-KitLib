using System;
using System.Linq;
using KitLib.Multiplayer.SyncBot;
using KitLib.Settings;
using KitLib.UI;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.Cheat;

/// <summary>Mutable target player selected by host MP picker.</summary>
internal sealed class MpCheatTargetPlayerRef {
    public Player Value { get; set; }
    public MpCheatTargetPlayerRef(Player initial) => Value = initial;
}

internal static class MpCheatUi {
    internal const string HooksTabId = "devmode.hooks";

    internal static bool IsHooksDisabledInMultiplayer =>
        MpCheatSession.InMultiplayerRun;

    /// <summary>Synced config toggles/sliders (host publishes; client sends ConfigRequest).</summary>
    internal static bool CanEditSyncedConfig =>
        !MpCheatSession.InMultiplayerRun || MpCheatSession.SessionArmed;

    /// <summary>Host-only commands (kill-all command publish, item host flows).</summary>
    internal static bool CanEditHostCommands =>
        !MpCheatSession.InMultiplayerRun || MpCheatSession.CanEditMultiplayerCheats;

    internal static void AddSessionBanner(VBoxContainer parent) {
        var label = new Label {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);

        if (!MpCheatSession.InMultiplayerRun) {
            if (!SettingsStore.Current.MultiplayerCheatOptIn)
                label.Text = I18N.T(
                    "mpcheat.optIn.hint",
                    "Enable Multiplayer Cheat in Dev Mode menu to sync cheats in co-op (all players need DevMode).");
            parent.AddChild(label);
            return;
        }

        if (MpCheatSession.CanUseMultiplayerCheats) {
            var baseText = MpCheatSession.IsHost
                ? I18N.T("mpcheat.host.active", "Multiplayer cheat: host — changes sync to all players.")
                : I18N.T("mpcheat.client.active", "Multiplayer cheat: client — cheat toggles sync via host.");
            if (MpCheatSession.IsHost && MpCheatSyncBot.IsEnabled)
                baseText += " " + I18N.T("syncbot.banner", "[SyncBot: simulated remote ACK/choices]");
            label.Text = baseText;
            label.AddThemeColorOverride("font_color", KitLibTheme.Accent);
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
        if (!CanEditSyncedConfig) return;
        setter(v);
        AfterCheatChanged();
    };

    internal static Action<float> WrapFloatSetter(Action<float> setter) => v => {
        if (!CanEditSyncedConfig) return;
        setter(v);
        AfterCheatChanged();
    };

    internal static void AfterCheatChanged() {
        if (!MpCheatSession.InMultiplayerRun) return;
        MpCheatState.ApplyOptimisticFromKitLibState();
        PlayerCheatEffects.ApplyImmediateIfEnabled();
        if (MpCheatSession.IsHost)
            MpCheatSync.HostPublishFromKitLibState("ui");
        else
            TaskHelper.RunSafely(MpCheatConfigCoordinator.TryClientPublishConfigAsync());
    }

    internal static bool IsFrameCheatAllowed => MpCheatApplier.FrameCheatsAllowed;

    internal static void ApplyMultiplayerUnsupported(Control row, string tooltipI18nKey, string tooltipFallback) {
        if (!MpCheatSession.InMultiplayerRun) return;
        DisableInteractiveDescendants(row);
        row.Modulate = new Color(0.55f, 0.55f, 0.55f, 0.85f);
        row.TooltipText = I18N.T(tooltipI18nKey, tooltipFallback);
    }

    private static void DisableInteractiveDescendants(Node node) {
        switch (node) {
            case BaseButton button:
                button.Disabled = true;
                break;
            case SpinBox spinBox:
                spinBox.Editable = false;
                break;
            case Slider slider:
                slider.Editable = false;
                break;
            case LineEdit lineEdit:
                lineEdit.Editable = false;
                break;
        }

        foreach (var child in node.GetChildren())
            DisableInteractiveDescendants(child);
    }

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
