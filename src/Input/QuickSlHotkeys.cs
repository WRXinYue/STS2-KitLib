using KitLib.Combat;
using KitLib.Multiplayer.Cheat;
using KitLib.Settings;
using KitLib.UI;
using Godot;
using MegaCrit.Sts2.Core.Combat;

namespace KitLib.Hotkeys;

/// <summary>Quick save/load and combat checkpoint hotkeys during an active run.</summary>
internal static class QuickSlHotkeys {
    internal static bool TryHandle(InputEvent @event, Viewport viewport) {
        if (!SettingsStore.Current.HotkeysEnabled)
            return false;
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return false;

        var settings = SettingsStore.Current;
        bool save = settings.HotkeyQuickSave.Matches(key);
        bool load = settings.HotkeyQuickLoad.Matches(key);
        bool replayCombat = settings.HotkeyQuickReplayCombat.Matches(key);
        bool replayTurn = settings.HotkeyQuickReplayTurn.Matches(key);
        if (!save && !load && !replayCombat && !replayTurn)
            return false;

        if (!RunContext.TryGetRunAndPlayer(out _, out _))
            return false;

        if (MpCheatSession.InMultiplayerRun) {
            QuickSlToastUI.Show(I18N.T("quickSl.multiplayerBlocked", "Quick save/load unavailable in multiplayer"));
            viewport.SetInputAsHandled();
            return true;
        }

        if (save) {
            if (SaveSlotManager.QuickSave())
                QuickSlToastUI.Show(I18N.T("quickSl.saved", "Quick save complete"));
            else
                QuickSlToastUI.Show(I18N.T("quickSl.failed", "Quick save failed"));
            viewport.SetInputAsHandled();
            return true;
        }

        if (replayCombat) {
            TryLoadCombatNode(viewport, CombatCheckpointKind.CombatStart,
                "quickSl.noCombatNode", "No combat checkpoint yet",
                "quickSl.replayCombat", "Replaying combat…");
            return true;
        }

        if (replayTurn) {
            TryLoadCombatNode(viewport, CombatCheckpointKind.TurnStart,
                "quickSl.noTurnNode", "No turn checkpoint yet",
                "quickSl.replayTurn", "Replaying turn…");
            return true;
        }

        if (!SaveSlotManager.HasQuickSnapshot) {
            QuickSlToastUI.Show(I18N.T("quickSl.noSnapshot", "No quick save yet"));
            viewport.SetInputAsHandled();
            return true;
        }

        if (SaveSlotManager.QuickLoad())
            QuickSlToastUI.Show(I18N.T("quickSl.loaded", "Loading quick save…"));
        viewport.SetInputAsHandled();
        return true;
    }

    private static void TryLoadCombatNode(
        Viewport viewport,
        CombatCheckpointKind kind,
        string missingKey,
        string missingFallback,
        string successKey,
        string successFallback) {
        if (CombatManager.Instance is not { IsInProgress: true }) {
            QuickSlToastUI.Show(I18N.T("quickSl.notInCombat", "Not in combat"));
            viewport.SetInputAsHandled();
            return;
        }

        if (!CombatCheckpointStore.HasNode(kind)) {
            QuickSlToastUI.Show(I18N.T(missingKey, missingFallback));
            viewport.SetInputAsHandled();
            return;
        }

        if (CombatCheckpointStore.TryLoadNode(kind))
            QuickSlToastUI.Show(I18N.T(successKey, successFallback));
        viewport.SetInputAsHandled();
    }
}
