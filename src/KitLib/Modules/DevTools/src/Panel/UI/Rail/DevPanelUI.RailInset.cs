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
    /// Keeps chrome readable inside the KitLib screen frame: the top bar's left-anchored player info,
    /// left-edge boxes (relic inventory / multiplayer bar) and the combat draw pile are pushed right by
    /// <see cref="RailInsetW"/>, which is also the width of the left contour band
    /// (<c>RailBackdrop</c>) where the screen frame's left border is widened to host the rail. The top
    /// bar is additionally pushed down and the hand container lifted by
    /// <see cref="KitLibScreenFrame.Margin"/>, and the boxes stacked under the top bar ride that same
    /// drop so their vanilla spacing survives. Background layers and the rest of the combat HUD are
    /// intentionally NOT moved — the band simply covers the left edge of the background, and
    /// right-anchored piles never run off-screen.
    /// </summary>
    internal const float RailInsetW = BrowserPanelLeft; // 76

    private static SceneTree? _railTree;
    private static bool _railInsetQueued;

    // HACK: temporary TopBar presence/visibility trace for the "official top bar vanishes mid-run" report.
    private static bool _diagMissing = true;
    private static bool _diagVisible;
    private static bool _diagInTree;
    private static Vector2 _diagSize;
    private static Node? _diagParent;
    private static float _diagWrapT = float.NaN;
    private static bool _diagWrapShown;

    // FullRect content pushed right by RailInsetW and down by the frame margin: the top bar.
    private static readonly List<(Control Ctrl, float Left, float Top, float LastLeft, float LastTop)> _leftContentInset = new();
    // Double-offset content nodes (left-anchored boxes that must keep their size): relic inventory /
    // multiplayer bar. Both edges move per axis, so the box translates instead of resizing.
    private static readonly List<(Control Ctrl, float Left, float Right, float Top, float Bottom, float LastLeft, float LastTop)> _leftContentShifted = new();

    // Bottom frame margin target: the hand container is the only node whose content reaches the frame edge.
    private static Control? _frameHand;
    private static float _frameHandBaseTop;
    private static float _frameHandBaseBottom;
    private static float _frameHandLastTop = float.NaN;

    internal static void AttachRailInset(NGlobalUi globalUi) {
        DetachRailInset();
        KitLog.Info("RailDiag", "AttachRailInset");
        _railTree = ((Node)globalUi).GetTree();
        _railTree.ProcessFrame += TickLeftInset;
    }

    internal static void DetachRailInset() {
        if (_railTree != null || _leftContentInset.Count > 0 || _leftContentShifted.Count > 0)
            KitLog.Info("RailDiag",
                $"DetachRailInset tree={_railTree != null} inset={_leftContentInset.Count} shifted={_leftContentShifted.Count}");
        if (_railTree != null) {
            _railTree.ProcessFrame -= TickLeftInset;
            _railTree = null;
        }
        _railInsetQueued = false;
        _diagMissing = true;
        _diagParent = null;
        _diagSize = Vector2.Zero;
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
        ApplyFrameBottomInset();
    }

    // Only chrome that must stay readable (player info top bar + left-edge boxes) is pushed right.
    // Background layers (RoomContainer / overlay / map) are intentionally left at x=0: the black
    // contour band covers their left edge, so they need no push.
    static void ApplyLeftContentInset() {
        var wanted = new List<Control>();

        // Both push amounts ride the wrap progress, so pushed chrome animates back to the real screen
        // edge in step with the frame retracting instead of snapping once the rail is gone.
        float railInset = RailInsetW * KitLibScreenFrame.WrapT;
        float frameMargin = KitLibScreenFrame.Margin * KitLibScreenFrame.WrapT;

        // Below the strip's taper only the narrow frame edge remains, so chrome down there keeps a
        // frame-width gap instead of the full rail inset.
        float InsetFor(Control? c) =>
            DevPanelUI.RailTaperEndY > 0f && c != null && c.GetGlobalRect().Position.Y >= DevPanelUI.RailTaperEndY
                ? frameMargin
                : railInset;

        // TopBar is a FullRect child of GlobalUi; shifting its OffsetLeft moves the left-anchored
        // player info right while keeping the right-anchored buttons pinned (their vanilla edge is
        // exactly 16px in), and shifting OffsetTop drops the whole strip below the frame's top edge.
        Control? topBar = null;
        if (_railGlobalUi is Control globalUiTop) {
            topBar = globalUiTop.GetNodeOrNull<Control>("TopBar");
            if (topBar != null)
                wanted.Add(topBar);
        }
        DiagTopBar(topBar);

        for (int i = _leftContentInset.Count - 1; i >= 0; i--) {
            var (ctrl, left, top, _, _) = _leftContentInset[i];
            if (wanted.Contains(ctrl))
                continue;
            if (GodotObject.IsInstanceValid(ctrl)) {
                ctrl.OffsetLeft = left;
                ctrl.OffsetTop = top;
            }
            _leftContentInset.RemoveAt(i);
        }

        foreach (var ctrl in wanted) {
            int i = _leftContentInset.FindIndex(s => s.Ctrl == ctrl);
            if (i < 0) {
                _leftContentInset.Add((ctrl, ctrl.OffsetLeft, ctrl.OffsetTop, float.NaN, float.NaN));
                i = _leftContentInset.Count - 1;
            }

            var (c, left, top, lastLeft, lastTop) = _leftContentInset[i];
            // Re-base each axis on its own: the game animates only OffsetTop (the bar's show/hide slide), and
            // merging the axes would re-base OffsetLeft on our own last write and accrue the inset per frame.
            bool gameOwnedLeft = float.IsNaN(lastLeft) || !Mathf.IsEqualApprox(c.OffsetLeft, lastLeft);
            bool gameOwnedTop = float.IsNaN(lastTop) || !Mathf.IsEqualApprox(c.OffsetTop, lastTop);
            if (gameOwnedLeft || gameOwnedTop) {
                if (!Mathf.IsEqualApprox(left, c.OffsetLeft) || !Mathf.IsEqualApprox(top, c.OffsetTop))
                    KitLog.Info("RailDiag",
                        $"{c.Name} game-owned base -> left={c.OffsetLeft} top={c.OffsetTop} (was left={left} top={top})");
                if (gameOwnedLeft)
                    left = c.OffsetLeft;
                if (gameOwnedTop)
                    top = c.OffsetTop;
            }

            float wantedLeft = left + InsetFor(c);
            float wantedTop = top + frameMargin;
            if (!Mathf.IsEqualApprox(c.OffsetLeft, wantedLeft))
                c.OffsetLeft = wantedLeft;
            if (!Mathf.IsEqualApprox(c.OffsetTop, wantedTop))
                c.OffsetTop = wantedTop;
            _leftContentInset[i] = (c, left, top, wantedLeft, wantedTop);
        }

        // Left-anchored boxes that must keep their size: shift both edges right, and drop them by the
        // same frame margin the top bar takes, so the bar keeps its gap to the row beneath it.
        if (_railGlobalUi is Control globalCtrl) {
            var relics = globalCtrl.GetNodeOrNull<Control>("RelicInventory");
            PushShifted(_leftContentShifted, relics, InsetFor(relics), frameMargin);
            if (globalCtrl is NGlobalUi g)
                PushShifted(_leftContentShifted, g.MultiplayerPlayerContainer, InsetFor(g.MultiplayerPlayerContainer), frameMargin);
        }

        // The draw pile is left-bottom anchored (x=15..95) and would sit under the rail strip,
        // so it is the only combat HUD node pushed right (80px width kept via double offset).
        // Discard / exhaust piles are right-anchored and stay put — no risk of running off-screen.
        if (NCombatRoom.Instance?.Ui is Control combatUi) {
            var piles = combatUi.GetNodeOrNull<Control>("%CombatPileContainer");
            var drawPile = piles?.GetNodeOrNull<Control>("%DrawPile");
            PushShifted(_leftContentShifted, drawPile, InsetFor(drawPile), 0f);
        }
    }

    static void RestoreLeftContentInset() {
        foreach (var (ctrl, left, top, _, _) in _leftContentInset) {
            if (GodotObject.IsInstanceValid(ctrl)) {
                ctrl.OffsetLeft = left;
                ctrl.OffsetTop = top;
            }
        }
        _leftContentInset.Clear();
        RestoreLeftShiftedList(_leftContentShifted);
        RestoreFrameBottomInset();
    }

    // The hand container is the only node whose content reaches the bottom frame edge; the piles,
    // end-turn button and energy counter all stop at least 6px short. Skipped while the replay bar
    // docks: its DockHeight already lifts the same nodes far above the frame, so the two never fight.
    static void ApplyFrameBottomInset() {
        if (CombatReplayBarUI.IsShowing) {
            RestoreFrameBottomInset();
            return;
        }

        var hand = NCombatRoom.Instance?.Ui is Control combatUi
            ? combatUi.GetNodeOrNull<Control>("%Hand")?.GetNodeOrNull<Control>("%CardHolderContainer")
            : null;
        if (hand == null || !GodotObject.IsInstanceValid(hand)) {
            RestoreFrameBottomInset();
            return;
        }

        if (_frameHand != hand) {
            RestoreFrameBottomInset();
            _frameHand = hand;
            _frameHandBaseTop = hand.OffsetTop;
            _frameHandBaseBottom = hand.OffsetBottom;
        }

        // The game owns the offset when it differs from what we last wrote -> re-capture the base.
        if (float.IsNaN(_frameHandLastTop) || !Mathf.IsEqualApprox(hand.OffsetTop, _frameHandLastTop)) {
            _frameHandBaseTop = hand.OffsetTop;
            _frameHandBaseBottom = hand.OffsetBottom;
        }

        // Lift rides the wrap progress so the hand settles back onto the screen edge as the frame leaves.
        float margin = KitLibScreenFrame.Margin * KitLibScreenFrame.WrapT;
        float wantedTop = _frameHandBaseTop - margin;
        float wantedBottom = _frameHandBaseBottom - margin;
        if (!Mathf.IsEqualApprox(hand.OffsetTop, wantedTop))
            hand.OffsetTop = wantedTop;
        if (!Mathf.IsEqualApprox(hand.OffsetBottom, wantedBottom))
            hand.OffsetBottom = wantedBottom;
        _frameHandLastTop = wantedTop;
    }

    static void RestoreFrameBottomInset() {
        if (_frameHand != null && GodotObject.IsInstanceValid(_frameHand)) {
            _frameHand.OffsetTop = _frameHandBaseTop;
            _frameHand.OffsetBottom = _frameHandBaseBottom;
        }
        _frameHand = null;
        _frameHandLastTop = float.NaN;
    }

    // Hand and right-anchored piles are intentionally NOT shifted horizontally: the black left contour
    // band covers the left edge (like the replay bar covers the bottom), so they never run off-screen.
    // Only the left-anchored draw pile is pushed right (see ApplyLeftContentInset); the hand is
    // lifted vertically instead (see ApplyFrameBottomInset).

    static void PushShifted(
        List<(Control Ctrl, float Left, float Right, float Top, float Bottom, float LastLeft, float LastTop)> list,
        Control? ctrl, float dx, float dy) {
        if (ctrl == null || !GodotObject.IsInstanceValid(ctrl))
            return;

        int i = list.FindIndex(s => s.Ctrl == ctrl);
        if (i < 0) {
            list.Add((ctrl, ctrl.OffsetLeft, ctrl.OffsetRight, ctrl.OffsetTop, ctrl.OffsetBottom,
                float.NaN, float.NaN));
            i = list.Count - 1;
        }

        var (c, left, right, top, bottom, lastLeft, lastTop) = list[i];
        // Re-base the edges on their own axis, same reason as ApplyLeftContentInset: a game-driven
        // vertical slide must not make the horizontal inset accrue.
        if (float.IsNaN(lastLeft) || !Mathf.IsEqualApprox(c.OffsetLeft, lastLeft)) {
            left = c.OffsetLeft;
            right = c.OffsetRight;
        }
        if (float.IsNaN(lastTop) || !Mathf.IsEqualApprox(c.OffsetTop, lastTop)) {
            top = c.OffsetTop;
            bottom = c.OffsetBottom;
        }

        float wantedLeft = left + dx;
        float wantedRight = right + dx;
        float wantedTop = top + dy;
        float wantedBottom = bottom + dy;
        if (!Mathf.IsEqualApprox(c.OffsetLeft, wantedLeft))
            c.OffsetLeft = wantedLeft;
        if (!Mathf.IsEqualApprox(c.OffsetRight, wantedRight))
            c.OffsetRight = wantedRight;
        if (!Mathf.IsEqualApprox(c.OffsetTop, wantedTop))
            c.OffsetTop = wantedTop;
        if (!Mathf.IsEqualApprox(c.OffsetBottom, wantedBottom))
            c.OffsetBottom = wantedBottom;
        list[i] = (c, left, right, top, bottom, wantedLeft, wantedTop);
    }

    static void RestoreLeftShiftedList(
        List<(Control Ctrl, float Left, float Right, float Top, float Bottom, float LastLeft, float LastTop)> list) {
        foreach (var (ctrl, left, right, top, bottom, _, _) in list) {
            if (GodotObject.IsInstanceValid(ctrl)) {
                ctrl.OffsetLeft = left;
                ctrl.OffsetRight = right;
                ctrl.OffsetTop = top;
                ctrl.OffsetBottom = bottom;
            }
        }
        list.Clear();
    }

    // HACK: logs only when the observed state changes so it does not spam; remove once the report is closed.
    static void DiagTopBar(Control? topBar) {
        float wrapT = KitLibScreenFrame.WrapT;
        bool wrapShown = KitLibScreenFrame.WrapShown;

        bool missing = topBar == null;
        bool visible = !missing && topBar!.Visible;
        bool inTree = !missing && topBar!.IsVisibleInTree();
        Vector2 size = missing ? Vector2.Zero : topBar!.Size;
        Node? parent = missing ? null : topBar!.GetParent();

        bool changed = missing != _diagMissing || visible != _diagVisible || inTree != _diagInTree ||
                       parent != _diagParent || !size.IsEqualApprox(_diagSize);
        if (changed) {
            _diagMissing = missing;
            _diagVisible = visible;
            _diagInTree = inTree;
            _diagSize = size;
            _diagParent = parent;
            KitLog.Info("RailDiag", missing
                ? $"TopBar MISSING (wrapT={wrapT:0.###} wrapShown={wrapShown})"
                : $"TopBar visible={visible} inTree={inTree} size={size} gpos={topBar!.GlobalPosition} "
                  + $"parent={(parent == null ? "null" : parent.Name.ToString())} (wrapT={wrapT:0.###} wrapShown={wrapShown})");
        }

        if (!Mathf.IsEqualApprox(wrapT, _diagWrapT) || wrapShown != _diagWrapShown) {
            _diagWrapT = wrapT;
            _diagWrapShown = wrapShown;
            KitLog.Info("RailDiag", $"wrap wrapT={wrapT:0.###} wrapShown={wrapShown}");
        }
    }
}
