using System.Collections.Generic;
using System.Linq;
using KitLib.CombatStats;
using KitLib.Settings;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.UI;

internal static partial class CombatStatsUI {
    internal const string MultiplayerOverlayRootName = "KitLibCombatStatsMpOverlay";

    private static MultiplayerOverlayHost? _mpOverlay;
    private static NGlobalUi? _mpOverlayGlobalUi;

    internal static bool IsMultiplayerOverlayActive() => _mpOverlay?.IsPanelVisible ?? false;

    internal static bool IsMultiplayerOverlayEnabled() =>
        SettingsStore.Current.CombatStatsMpOverlayEnabled;

    internal static bool CanShowMultiplayerOverlay() =>
        ShouldUseMultiplayerOverlay() && IsMultiplayerOverlayEnabled();

    internal static void SyncMultiplayerOverlayState(NGlobalUi? globalUi = null) {
        if (globalUi != null)
            _mpOverlayGlobalUi = globalUi;
        Callable.From(ApplyMultiplayerOverlayState).CallDeferred();
    }

    private static void ApplyMultiplayerOverlayState() {
        EnsureMultiplayerOverlayAttached();

        if (!IsMultiplayerOverlayEnabled() || !ShouldUseMultiplayerOverlay()) {
            HideMultiplayerOverlay();
            return;
        }

        RefreshMultiplayerOverlay();
    }

    internal static bool ShouldUseMultiplayerOverlay() {
        var run = RunManager.Instance;
        if (run?.IsInProgress != true)
            return false;
        if (run.NetService?.Type == NetGameType.Singleplayer)
            return false;

        var combat = CombatManager.Instance?.DebugOnlyGetState();
        if (combat != null && combat.Players.Count > 1)
            return true;

        var state = run.DebugOnlyGetState();
        return state != null && state.Players.Count > 1;
    }

    internal static void AttachMultiplayerOverlay(NGlobalUi globalUi) {
        _mpOverlayGlobalUi = globalUi;
        EnsureMultiplayerOverlayAttached();
    }

    internal static void DetachMultiplayerOverlay(NGlobalUi globalUi) {
        ((Node)globalUi).GetNodeOrNull<Control>(MultiplayerOverlayRootName)?.QueueFree();
        _mpOverlay = null;
        if (_mpOverlayGlobalUi == globalUi)
            _mpOverlayGlobalUi = null;
    }

    internal static void RefreshMultiplayerOverlay() {
        if (_mpOverlay == null || !GodotObject.IsInstanceValid(_mpOverlay))
            EnsureMultiplayerOverlayAttached();
        _mpOverlay?.Refresh();
    }

    internal static void HideMultiplayerOverlay() => _mpOverlay?.HidePanel();

    internal static List<PlayerCombatStats> ResolveOverlayPlayers() {
        if (CombatStatsTracker.IsTracking && CombatStatsTracker.Current.Players.Count >= 2)
            return CombatStatsTracker.Current.Players.Values.ToList();

        if (CombatStatsTracker.Last?.Players.Count >= 2)
            return CombatStatsTracker.Last.Players.Values.ToList();

        if (CombatStatsTracker.RunTotal.Players.Count >= 2)
            return CombatStatsTracker.RunTotal.Players.Values.ToList();

        var snap = CombatStatsTracker.IsTracking
            ? CombatStatsTracker.Current
            : CombatStatsTracker.Last;
        return snap?.Players.Values.ToList() ?? new List<PlayerCombatStats>();
    }

    private static void EnsureMultiplayerOverlayAttached() {
        if (_mpOverlayGlobalUi == null)
            return;

        var parent = (Node)_mpOverlayGlobalUi;
        var existing = parent.GetNodeOrNull<Control>(MultiplayerOverlayRootName);
        if (existing is MultiplayerOverlayHost host && GodotObject.IsInstanceValid(host)) {
            _mpOverlay = host;
            return;
        }

        if (_mpOverlay != null && GodotObject.IsInstanceValid(_mpOverlay) && _mpOverlay.IsInsideTree()) {
            if (_mpOverlay.GetParent() == parent)
                return;
        }

        _mpOverlay = null;
        var overlay = new MultiplayerOverlayHost();
        _mpOverlay = overlay;
        parent.AddChild(overlay);
        overlay.TreeExiting += () => {
            if (_mpOverlay == overlay)
                _mpOverlay = null;
        };
    }
}
