using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    /// <summary>
    /// Keeps left-edge chrome readable next to the rail: the top bar's left-anchored player info,
    /// left-edge boxes (relic inventory / multiplayer bar) and the combat draw pile are pushed right
    /// by <see cref="RailInsetW"/>. Background layers and the rest of the combat HUD are intentionally
    /// NOT moved — the frosted rail backdrop (<see cref="RailBackdrop"/>) overlays real game content,
    /// so there is something to blur, and right-anchored piles never run off-screen.
    /// </summary>
    internal const float RailInsetW = BrowserPanelLeft; // 76

    private static SceneTree? _railTree;
    private static bool _railInsetQueued;
    // OffsetLeft-only content nodes (FullRect): RoomContainer / overlay / map.
    private static readonly List<(Control Ctrl, float Left, float LastLeft)> _leftContentInset = new();
    // Double-offset content nodes (left-anchored boxes that must keep width): relic inventory / multiplayer bar.
    private static readonly List<(Control Ctrl, float Left, float Right, float LastLeft)> _leftContentShifted = new();

    internal static void AttachRailInset(NGlobalUi globalUi) {
        DetachRailInset();
        _railTree = ((Node)globalUi).GetTree();
        _railTree.ProcessFrame += TickLeftInset;
    }

    internal static void DetachRailInset() {
        if (_railTree != null) {
            _railTree.ProcessFrame -= TickLeftInset;
            _railTree = null;
        }
        _railInsetQueued = false;
        RestoreLeftContentInset();
    }

    static void TickLeftInset() {
        if (_railInsetQueued || _railTree == null)
            return;
        _railInsetQueued = true;
        Callable.From(ApplyLeftInsets).CallDeferred();
    }

    static void ApplyLeftInsets() {
        _railInsetQueued = false;
        if (_railTree == null)
            return;

        ApplyLeftContentInset();
    }

    // Only chrome that must stay readable (player info top bar + left-edge boxes) is pushed right.
    // Background layers (RoomContainer / overlay / map) are intentionally left at x=0 so the frosted
    // rail backdrop (DevPanelUI.RailBackdrop) has real game content behind it to blur.
    static void ApplyLeftContentInset() {
        var wanted = new List<Control>();

        // TopBar is a FullRect child of GlobalUi; shifting only its OffsetLeft moves the
        // left-anchored player info right while keeping the right-anchored buttons pinned.
        if (_railGlobalUi is Control globalUiTop) {
            var topBar = globalUiTop.GetNodeOrNull<Control>("TopBar");
            if (topBar != null)
                wanted.Add(topBar);
        }

        for (int i = _leftContentInset.Count - 1; i >= 0; i--) {
            var (ctrl, left, _) = _leftContentInset[i];
            if (wanted.Contains(ctrl))
                continue;
            if (GodotObject.IsInstanceValid(ctrl))
                ctrl.OffsetLeft = left;
            _leftContentInset.RemoveAt(i);
        }

        foreach (var ctrl in wanted) {
            int i = _leftContentInset.FindIndex(s => s.Ctrl == ctrl);
            if (i < 0) {
                _leftContentInset.Add((ctrl, ctrl.OffsetLeft, float.NaN));
                i = _leftContentInset.Count - 1;
            }

            var (c, left, lastLeft) = _leftContentInset[i];
            bool gameOwned = float.IsNaN(lastLeft) || !Mathf.IsEqualApprox(c.OffsetLeft, lastLeft);
            if (gameOwned)
                left = c.OffsetLeft;

            float wantedLeft = left + RailInsetW;
            if (!Mathf.IsEqualApprox(c.OffsetLeft, wantedLeft))
                c.OffsetLeft = wantedLeft;
            _leftContentInset[i] = (c, left, wantedLeft);
        }

        // Left-anchored boxes that must keep their width: shift both edges right.
        if (_railGlobalUi is Control globalCtrl) {
            PushLeftShifted(_leftContentShifted, globalCtrl.GetNodeOrNull<Control>("RelicInventory"));
            if (globalCtrl is NGlobalUi g)
                PushLeftShifted(_leftContentShifted, g.MultiplayerPlayerContainer);
        }

        // The draw pile is left-bottom anchored (x=15..95) and would sit under the rail strip,
        // so it is the only combat HUD node pushed right (80px width kept via double offset).
        // Discard / exhaust piles are right-anchored and stay put — no risk of running off-screen.
        if (NCombatRoom.Instance?.Ui is Control combatUi) {
            var piles = combatUi.GetNodeOrNull<Control>("%CombatPileContainer");
            PushLeftShifted(_leftContentShifted, piles?.GetNodeOrNull<Control>("%DrawPile"));
        }
    }

    static void RestoreLeftContentInset() {
        foreach (var (ctrl, left, _) in _leftContentInset) {
            if (GodotObject.IsInstanceValid(ctrl))
                ctrl.OffsetLeft = left;
        }
        _leftContentInset.Clear();
        RestoreLeftShiftedList(_leftContentShifted);
    }

    // Hand and right-anchored piles are intentionally NOT shifted: the frosted rail overlays the
    // left edge (like the replay bar overlays the bottom), so they never run off-screen. Only the
    // left-anchored draw pile is pushed right (see ApplyLeftContentInset).

    static void PushLeftShifted(List<(Control Ctrl, float Left, float Right, float LastLeft)> list, Control? ctrl) {
        if (ctrl == null || !GodotObject.IsInstanceValid(ctrl))
            return;

        int i = list.FindIndex(s => s.Ctrl == ctrl);
        if (i < 0) {
            list.Add((ctrl, ctrl.OffsetLeft, ctrl.OffsetRight, float.NaN));
            i = list.Count - 1;
        }

        var (c, left, right, lastLeft) = list[i];
        bool gameOwned = float.IsNaN(lastLeft) || !Mathf.IsEqualApprox(c.OffsetLeft, lastLeft);
        if (gameOwned) {
            left = c.OffsetLeft;
            right = c.OffsetRight;
        }

        float wantedLeft = left + RailInsetW;
        float wantedRight = right + RailInsetW;
        if (!Mathf.IsEqualApprox(c.OffsetLeft, wantedLeft))
            c.OffsetLeft = wantedLeft;
        if (!Mathf.IsEqualApprox(c.OffsetRight, wantedRight))
            c.OffsetRight = wantedRight;
        list[i] = (c, left, right, wantedLeft);
    }

    static void RestoreLeftShiftedList(List<(Control Ctrl, float Left, float Right, float LastLeft)> list) {
        foreach (var (ctrl, left, right, _) in list) {
            if (GodotObject.IsInstanceValid(ctrl)) {
                ctrl.OffsetLeft = left;
                ctrl.OffsetRight = right;
            }
        }
        list.Clear();
    }
}
