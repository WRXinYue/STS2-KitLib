using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    private const string BrowserPanelAnimatingMetaKey = "_dm_browser_panel_animating";
    internal const string BrowserPanelClosingMetaKey = "_dm_browser_panel_closing";
    private const string BrowserPanelClipHostName = "BrowserPanelClipHost";
    /// <summary>
    /// Creates a full-screen browser overlay shell with rail management and optional backdrop.
    /// </summary>
    /// <param name="globalUi">The global UI context used for rail splicing.</param>
    /// <param name="rootName">Unique name for the root control, used for persistence and identification.</param>
    /// <param name="panelWidth">
    /// Desired panel width in pixels. Use <c>0</c> for full-width panel.
    /// Values greater than <c>0</c> will automatically add a click-outside backdrop.
    /// </param>
    /// <param name="onClose">Callback invoked when the backdrop is clicked or the overlay is dismissed.</param>
    /// <param name="contentSeparation">Vertical spacing between content elements in the panel. Default is <c>10</c>.</param>
    /// <param name="zIndex">Rendering order for the root control. Higher values appear on top. Default is <c>1250</c>.</param>
    /// <param name="backdropWhenFullWidth">
    /// When <c>true</c>, adds a backdrop even if <paramref name="panelWidth"/> is <c>0</c>.
    /// Useful for card browsers and encounter pickers that need click-outside behavior.
    /// </param>
    /// <returns>
    /// A tuple containing:
    /// <list type="bullet">
    /// <item><description><c>Root</c> - The full-screen container control</description></item>
    /// <item><description><c>Panel</c> - The browser panel with width grip attached</description></item>
    /// <item><description><c>Content</c> - The VBoxContainer for adding browser content</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="globalUi"/>, <paramref name="rootName"/>, or <paramref name="onClose"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="panelWidth"/> is negative.
    /// </exception>
    /// <remarks>
    /// <para>This method manages rail pinning and splicing automatically. The rail is pinned
    /// when the overlay is created and unpinned when the root control exits the scene tree.</para>
    /// <para>Panel width is automatically persisted and restored via <see cref="KitLibSettings"/>.
    /// A width grip is attached to the right edge for user resizing.</para>
    /// </remarks>
    internal static (Control Root, PanelContainer Panel, VBoxContainer Content) CreateBrowserOverlayShell(
        NGlobalUi globalUi,
        string rootName,
        float panelWidth,
        Action onClose,
        int contentSeparation = 10,
        int zIndex = 1250,
        bool backdropWhenFullWidth = false) {
        ArgumentNullException.ThrowIfNull(globalUi);
        ArgumentNullException.ThrowIfNull(rootName);
        ArgumentNullException.ThrowIfNull(onClose);

        if (panelWidth < 0)
            throw new ArgumentOutOfRangeException(nameof(panelWidth), "Panel width cannot be negative");

        var root = CreateAndSetupRoot(globalUi, rootName, zIndex);
        void RequestClose() => RequestCloseBrowserOverlay(globalUi, rootName, onClose);

        float resolved = ResolveBrowserPanelWidth(rootName, panelWidth, (Node)globalUi);
        if (resolved > 0f || backdropWhenFullWidth)
            root.AddChild(CreateBrowserBackdrop(RequestClose));

        var panel = CreateBrowserPanel(resolved);
        AddPanelToRoot(root, panel, rootName, globalUi);

        var content = GetPanelContent(panel, contentSeparation);

        return (root, panel, content);
    }

    /// <summary>
    /// Creates a browser overlay shell using a pre-configured <see cref="PanelContainer"/>.
    /// </summary>
    /// <param name="globalUi">The global UI context used for rail splicing.</param>
    /// <param name="rootName">Unique name for the root control, used for persistence and identification.</param>
    /// <param name="panel">
    /// A pre-configured <see cref="PanelContainer"/> instance.
    /// Use this overload for custom panels with specific margins (e.g., card or relic browsers).
    /// </param>
    /// <param name="onClose">Callback invoked when the backdrop is clicked or the overlay is dismissed.</param>
    /// <param name="contentSeparation">Vertical spacing between content elements in the panel.</param>
    /// <param name="addBackdrop">
    /// When <c>true</c> (default), adds a click-outside backdrop.
    /// Set to <c>false</c> for overlays that don't need dismissal behavior.
    /// </param>
    /// <param name="zIndex">Rendering order for the root control. Default is <c>1250</c>.</param>
    /// <returns>
    /// A tuple containing:
    /// <list type="bullet">
    /// <item><description><c>Root</c> - The full-screen container control</description></item>
    /// <item><description><c>Panel</c> - The custom panel with width settings applied</description></item>
    /// <item><description><c>Content</c> - The VBoxContainer for adding browser content</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="globalUi"/>, <paramref name="rootName"/>, <paramref name="panel"/>, or <paramref name="onClose"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the <paramref name="panel"/> does not contain a child named "Content" of type <see cref="VBoxContainer"/>.
    /// </exception>
    /// <remarks>
    /// <para>Unlike the other overload, this method does not create a new panel but uses the provided one.
    /// Width persistence and grip registration are still applied automatically.</para>
    /// <para>The panel should have a direct child named "Content" of type <see cref="VBoxContainer"/>.</para>
    /// </remarks>
    internal static (Control Root, PanelContainer Panel, VBoxContainer Content) CreateBrowserOverlayShell(
        NGlobalUi globalUi,
        string rootName,
        PanelContainer panel,
        Action onClose,
        int contentSeparation,
        bool addBackdrop = true,
        int zIndex = 1250) {
        ArgumentNullException.ThrowIfNull(globalUi);
        ArgumentNullException.ThrowIfNull(rootName);
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(onClose);

        var root = CreateAndSetupRoot(globalUi, rootName, zIndex);
        void RequestClose() => RequestCloseBrowserOverlay(globalUi, rootName, onClose);

        if (addBackdrop)
            root.AddChild(CreateBrowserBackdrop(RequestClose));

        AddPanelToRoot(root, panel, rootName, globalUi);

        var content = GetPanelContent(panel, contentSeparation);

        return (root, panel, content);
    }

    #region Private Helpers

    internal static Control CreateAndSetupRoot(NGlobalUi globalUi, string rootName, int zIndex) {
        var root = new Control { Name = rootName, MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = zIndex };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        SetupRailTransition(globalUi, root);
        return root;
    }

    private static void SetupRailTransition(NGlobalUi globalUi, Control root) {
        _browserOverlayCount++;
        PinRail();
        ReconcileBrowserRail(globalUi);
        CombatStatsUI.SyncMultiplayerOverlayState(globalUi);
        MonsterIntentOverlayUI.SyncState(globalUi);
        root.TreeExiting += () => {
            _browserOverlayCount = Math.Max(0, _browserOverlayCount - 1);
            UnpinRail();
            ReconcileBrowserRail(globalUi);
            CombatStatsUI.SyncMultiplayerOverlayState(globalUi);
            MonsterIntentOverlayUI.SyncState(globalUi);
            if (root.Name == LogCollector.LogViewerRootName) {
                Callable.From(() => {
                    LogCollector.SyncLogViewerOpen(globalUi);
                    RefreshRailHintPresentation();
                }).CallDeferred();
            }
        };
    }

    private static void AddPanelToRoot(Control root, PanelContainer panel, string rootName, NGlobalUi globalUi) {
        ApplyInitialBrowserWidthFromSettings((Node)globalUi, panel, rootName);
        RebasePanelLayoutForClipHost(panel);

        bool played = false;
        panel.TreeEntered += () => {
            if (played) return;
            played = true;
            PlayBrowserPanelOpenAnimation(panel);
        };
        var clipHost = CreateBrowserPanelClipHost();
        clipHost.AddChild(panel);
        root.AddChild(clipHost);
        RegisterBrowserPanelWidthGrip(root, panel, rootName);
        WireBrowserPanelMergeTracking(globalUi, panel);
    }

    private static VBoxContainer GetPanelContent(PanelContainer panel, int contentSeparation) {
        var content = panel.GetNodeOrNull<VBoxContainer>("Content")
            ?? throw new InvalidOperationException($"Panel '{panel.Name}' is missing a 'Content' VBoxContainer child");

        content.AddThemeConstantOverride("separation", contentSeparation);
        return content;
    }

    private static void PlayBrowserPanelOpenAnimation(PanelContainer panel) =>
        PlayBrowserPanelSlideInFromLeft(panel, 0.82f);

    /// <summary>Second column in a flush join (e.g. save slots): same motion as the main browser — enters from the left.</summary>
    internal static void PlayBrowserPanelOpenFromLeft(PanelContainer panel) =>
        PlayBrowserPanelSlideInFromLeft(panel, 0.82f);

    private static void PlayBrowserPanelSlideInFromLeft(PanelContainer panel, float durationSec) {
        if (!panel.IsInsideTree())
            return;

        float panelWidth = panel.GetRect().Size.X;
        if (panelWidth < 1f)
            return;

        panel.SetMeta(BrowserPanelAnimatingMetaKey, true);
        float targetX = panel.Position.X;
        float startX = targetX - panelWidth;
        panel.Position = new Vector2(startX, panel.Position.Y);

        var t = panel.CreateTween();
        t.SetTrans(Tween.TransitionType.Quint);
        t.SetEase(Tween.EaseType.Out);
        t.TweenProperty(panel, "position:x", targetX, durationSec);
        t.Chain()
            .TweenCallback(Callable.From(() => {
                panel.SetMeta(BrowserPanelAnimatingMetaKey, false);
                var root = panel.GetParentOrNull<Control>();
                if (root?.GetParent() is Control parentRoot)
                    root = parentRoot;
                root?.GetNodeOrNull<BrowserPanelWidthGrip>("PanelWidthGrip")?.Sync();
                if (_mergeGlobalUi != null)
                    NotifyBrowserContextLayoutChanged(_mergeGlobalUi);
            }));
    }

    internal static Control CreateBrowserPanelClipHost() {
        var clipHost = new Control {
            Name = BrowserPanelClipHostName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = true
        };
        clipHost.AnchorLeft = 0;
        clipHost.AnchorRight = 1;
        clipHost.AnchorTop = 0;
        clipHost.AnchorBottom = 1;
        clipHost.OffsetLeft = BrowserPanelLeft;
        clipHost.OffsetRight = 0;
        clipHost.OffsetTop = 0;
        clipHost.OffsetBottom = 0;
        return clipHost;
    }

    private static void RebasePanelLayoutForClipHost(PanelContainer panel) {
        panel.OffsetLeft -= BrowserPanelLeft;
        if (panel.AnchorRight < 0.5f)
            panel.OffsetRight -= BrowserPanelLeft;
    }

    internal static void RequestCloseBrowserOverlay(NGlobalUi globalUi, string rootName, Action fallbackClose) {
        // User explicitly closed the panel — tell the controller so the tab can be reopened.
        _controller.Deactivate();

        var parent = (Node)globalUi;
        var root = parent.GetNodeOrNull<Control>(rootName);
        if (root == null) {
            fallbackClose();
            return;
        }

        if (TryAnimateBrowserOverlayClose(parent, root))
            return;

        fallbackClose();
    }

    internal static bool TryAnimateBrowserOverlayClose(Node parent, Control root) {
        if (IsDualColumnOverlay(root))
            return TryAnimateDualPanelClose(parent, root);

        var clipHost = root.GetNodeOrNull<Control>(BrowserPanelClipHostName);
        var panel = clipHost?.GetNodeOrNull<PanelContainer>("BrowserPanel");
        if (panel == null)
            return false;

        if (root.HasMeta(BrowserPanelClosingMetaKey) && root.GetMeta(BrowserPanelClosingMetaKey).AsBool())
            return true;

        root.SetMeta(BrowserPanelClosingMetaKey, true);
        root.SetMeta(BrowserPanelAnimatingMetaKey, true);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.GetNodeOrNull<BrowserPanelWidthGrip>("PanelWidthGrip")?.Hide();

        float startX = panel.Position.X;
        float endX = startX - MathF.Max(1f, panel.GetRect().Size.X);

        var tween = panel.CreateTween();
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.SetEase(Tween.EaseType.In);
        tween.TweenProperty(panel, "position:x", endX, 0.22f);
        tween.Chain().TweenCallback(Callable.From(() => {
            root.SetMeta(BrowserPanelAnimatingMetaKey, false);
            if (root.IsInsideTree()) {
                var p = root.GetParent();
                p?.RemoveChild(root);
                root.QueueFree();
            }
        }));

        return true;
    }

    private static bool IsDualColumnOverlay(Control root) {
        if (!root.HasMeta(DualCarrierMetaKey))
            return false;
        var name = root.GetMeta(DualCarrierMetaKey).AsString();
        return !string.IsNullOrEmpty(name);
    }

    private static bool TryAnimateDualPanelClose(Node parent, Control root) {
        var clipHost = root.GetNodeOrNull<Control>(BrowserPanelClipHostName);
        if (!root.HasMeta(DualCarrierMetaKey))
            return false;
        var carrierName = root.GetMeta(DualCarrierMetaKey).AsString();
        var mover = clipHost?.GetNodeOrNull<Control>(carrierName);
        if (mover == null)
            return false;

        if (root.HasMeta(BrowserPanelClosingMetaKey) && root.GetMeta(BrowserPanelClosingMetaKey).AsBool())
            return true;

        root.SetMeta(BrowserPanelClosingMetaKey, true);
        root.SetMeta(BrowserPanelAnimatingMetaKey, true);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.GetNodeOrNull<BrowserPanelWidthGrip>("PanelWidthGrip")?.Hide();

        float startX = mover.Position.X;
        float w = Mathf.Max(1f, mover.GetRect().Size.X);
        float endX = startX - w;

        var tween = mover.CreateTween();
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.SetEase(Tween.EaseType.In);
        tween.TweenProperty(mover, "position:x", endX, 0.26f);
        tween.Chain().TweenCallback(Callable.From(() => {
            root.SetMeta(BrowserPanelAnimatingMetaKey, false);
            if (root.IsInsideTree()) {
                var p = root.GetParent();
                p?.RemoveChild(root);
                root.QueueFree();
            }
        }));

        return true;
    }

    internal static void PlaySubPanelSlideOpenFromLeft(Control mover) {
        if (!mover.IsInsideTree())
            return;

        float w = Mathf.Max(1f, mover.GetRect().Size.X);
        if (w < 2f)
            return;

        mover.SetMeta(BrowserPanelAnimatingMetaKey, true);
        float targetX = mover.Position.X;
        float startX = targetX - w;
        mover.Position = new Vector2(startX, mover.Position.Y);

        var t = mover.CreateTween();
        t.SetTrans(Tween.TransitionType.Quint);
        t.SetEase(Tween.EaseType.Out);
        t.TweenProperty(mover, "position:x", targetX, 0.82f);
        t.Chain()
            .TweenCallback(Callable.From(() => {
                mover.SetMeta(BrowserPanelAnimatingMetaKey, false);
                mover.GetParent()?.GetParent()?.GetNodeOrNull<BrowserPanelWidthGrip>("PanelWidthGrip")?.Sync();
            }));
    }

    #endregion
}
