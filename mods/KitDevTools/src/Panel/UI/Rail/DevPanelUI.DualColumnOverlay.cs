using System;
using Godot;
using KitLib.Icons;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    internal const string DualCarrierMetaKey = "dm_dual_carrier_name";
    internal const string MoverFreePositionMetaKey = "_dm_mover_free";

    internal sealed class DualColumnOverlayOptions {
        public required NGlobalUi GlobalUi { get; init; }
        public required string RootName { get; init; }
        public required string DualMetaKey { get; init; }
        public required string CarrierNodeName { get; init; }
        public required Action FallbackClose { get; init; }
        public float MainDefaultWidth { get; init; } = 520f;
        public bool MainUseMaxWidth { get; init; }
        public float ExtDefaultWidth { get; init; } = 420f;
        public float ExtSlideOutSec { get; init; } = 0.28f;
        public int ZIndex { get; init; } = BrowserOverlayZIndex;
        /// <summary>Header title; when empty, only the drag bar + collapse button are shown.</summary>
        public string? HeaderTitle { get; init; }
        /// <summary>Enables the unified drag + collapse button for this content panel (enabled by default for every panel).</summary>
        public bool EnableDragHide { get; init; } = true;
        /// <summary>Invoked once after the open slide finishes (or immediately if the slide is skipped).</summary>
        public Action? OnOpenAnimationFinished { get; init; }
    }

    internal sealed class DualColumnOverlayHandle {
        private readonly DualColumnOverlayOptions _options;
        private readonly Control _clipHost;
        private readonly float _nominalMainW;
        private readonly float _nominalExtW;
        private Tween? _extCloseTween;
        private FloatingCombatOverlay.DraggablePanelBinding? _dragBinding;
        private bool _freePosition;

        internal Control Root { get; }
        internal VBoxContainer MainContent { get; }
        internal VBoxContainer ExtContent { get; }
        internal PanelContainer MainPanel { get; }
        internal PanelContainer ExtPanel { get; }
        internal Control ExtSlot { get; }
        internal Control ExtSlideHost { get; }
        internal Control Mover { get; }

        internal DualColumnOverlayHandle(
            DualColumnOverlayOptions options,
            Control root,
            Control clipHost,
            Control mover,
            PanelContainer mainPanel,
            VBoxContainer mainContent,
            VBoxContainer extContent,
            PanelContainer extPanel,
            Control extSlot,
            Control extSlideHost,
            float nominalMainW,
            float nominalExtW) {
            _options = options;
            Root = root;
            _clipHost = clipHost;
            _nominalMainW = nominalMainW;
            _nominalExtW = nominalExtW;
            Mover = mover;
            MainPanel = mainPanel;
            MainContent = mainContent;
            ExtContent = extContent;
            ExtPanel = extPanel;
            ExtSlot = extSlot;
            ExtSlideHost = extSlideHost;
        }

        internal void AttachToScene() {
            bool opened = false;
            _clipHost.TreeEntered += () => {
                if (opened) return;
                opened = true;
                Callable.From(() => {
                    SyncMoverWidth();
                    PlaySubPanelSlideOpenFromLeft(Mover, _options.OnOpenAnimationFinished);
                }).CallDeferred();
            };

            ((Node)_options.GlobalUi).AddChild(Root);
            // Match rail chrome: viewport-level so vanilla fullscreen modal blockers cannot eat panel clicks.
            Root.TopLevel = true;
        }

        /// <summary>
        /// Wires the unified header (title + drag-by-title + persistent-hide collapse button)
        /// into the content panel. Reuses <see cref="FloatingCombatOverlay.DraggablePanelBinding"/>;
        /// collapse goes through <see cref="DevPanelUI.HideBrowserOverlayPersistent"/> and the rail
        /// click restores the panel to its docked spot.
        /// </summary>
        internal void WireDragAndHide(NGlobalUi globalUi, string? title) {
            // ── Unified header: inserted at the top of MainContent ──
            var header = new HBoxContainer { CustomMinimumSize = new Vector2(0, 30) };
            header.AddThemeConstantOverride("separation", 8);
            header.MouseFilter = Control.MouseFilterEnum.Stop;

            var titleLabel = new Label {
                Text = string.IsNullOrEmpty(title) ? " " : title,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                ClipText = true,
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 13);
            titleLabel.AddThemeColorOverride("font_color", KitLibTheme.Accent);
            header.AddChild(titleLabel);

            // expand spacer sits between title and collapse so only the left/title area drags;
            // the collapse button stays clickable.
            header.AddChild(new Control());

            var collapseBtn = DevPanelUI.CreateHeaderIconButton(MdiIcon.Minus, I18N.T("panel.header.collapse", "隐藏（收起，常驻）"));
            collapseBtn.Pressed += () => DevPanelUI.HideBrowserOverlayPersistent(globalUi, Root);
            header.AddChild(collapseBtn);

            MainContent.AddChild(header);
            MainContent.MoveChild(header, 0);

            // ── Drag binding: drag to a free position; clipHost clips anything dragged past the left edge ──
            float defaultWidth = Mathf.Max(1f, _nominalMainW + _nominalExtW);
            _dragBinding = new FloatingCombatOverlay.DraggablePanelBinding(
                host: _clipHost,
                panel: Mover,
                defaultWidth: defaultWidth,
                isFreePosition: () => _freePosition,
                setFreePosition: v => {
                    _freePosition = v;
                    if (v)
                        Mover.SetMeta(MoverFreePositionMetaKey, true);
                    else
                        Mover.RemoveMeta(MoverFreePositionMetaKey);
                });
            _dragBinding.WireHandle(header);

            // The open slide animation is driven by mover.position:x; grabbing is disabled while it runs.
            header.MouseFilter = Control.MouseFilterEnum.Ignore;
            Root.TreeEntered += () => {
                Callable.From(() => {
                    if (!GodotObject.IsInstanceValid(header))
                        return;
                    // Re-enable dragging only after the slide-in (~0.82s) finishes.
                    var t = new Godot.Timer { WaitTime = 0.9, OneShot = true, Autostart = true };
                    t.Timeout += () => {
                        if (GodotObject.IsInstanceValid(header))
                            header.MouseFilter = Control.MouseFilterEnum.Stop;
                    };
                    Root.AddChild(t);
                }).CallDeferred();
            };

            // The header is interactive, so it needs a host that drives the drag binding every frame.
            var processHost = new DragProcessHost { Binding = _dragBinding };
            Root.AddChild(processHost);
        }

        internal void OpenExtension(bool toggleIfOpen = false) {
            Callable.From(() => {
                if (ExtSlot.Visible) {
                    if (toggleIfOpen)
                        CloseExtension();
                    return;
                }

                PrepareExtensionVisible();
                Callable.From(AnimateExtensionSlideIn).CallDeferred();
            }).CallDeferred();
        }

        internal void ToggleExtension() => OpenExtension(toggleIfOpen: true);

        internal void PrepareExtensionVisible() {
            KillExtCloseTween();
            ExtSlideHost.Position = Vector2.Zero;
            ExtPanel.Position = Vector2.Zero;
            ExtPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            ExtSlot.Visible = true;
            SyncMoverWidth();
        }

        internal void AnimateExtensionSlideIn() => PlayControlSlideOpenFromLeft(ExtSlideHost);

        internal void CloseExtension(Action? onHidden = null) {
            if (!ExtSlot.Visible) return;
            KillExtCloseTween();
            float w = Mathf.Max(1f, ExtSlideHost.GetRect().Size.X);
            _extCloseTween = ExtSlideHost.CreateTween();
            _extCloseTween.SetTrans(Tween.TransitionType.Cubic);
            _extCloseTween.SetEase(Tween.EaseType.In);
            _extCloseTween.TweenProperty(ExtSlideHost, "position:x", w, _options.ExtSlideOutSec);
            _extCloseTween.TweenCallback(Callable.From(() => {
                _extCloseTween = null;
                onHidden?.Invoke();
                ExtSlideHost.Position = Vector2.Zero;
                ExtSlot.Visible = false;
                SyncMoverWidth();
            }));
        }

        internal void KillExtCloseTween() {
            _extCloseTween?.Kill();
            _extCloseTween = null;
        }

        internal void SyncMoverWidth() {
            float totalW = _nominalMainW + (ExtSlot.Visible ? _nominalExtW : 0f);
            // Centered panel: offsets keep the mover horizontally centered for its current width.
            Mover.OffsetLeft = -totalW * 0.5f;
            Mover.OffsetRight = totalW * 0.5f;
        }
    }

    internal static DualColumnOverlayHandle CreateDualColumnOverlay(DualColumnOverlayOptions options) {
        var globalUi = options.GlobalUi;
        var root = CreateAndSetupRoot(globalUi, options.RootName, options.ZIndex);
        root.SetMeta(options.DualMetaKey, true);
        root.SetMeta(DualCarrierMetaKey, options.CarrierNodeName);

        root.AddChild(CreateBrowserBackdrop(
            () => RequestCloseBrowserOverlay(globalUi, options.RootName, options.FallbackClose)));

        float mainW;
        float extW;
        (mainW, extW) = ResolveDualColumnWidths(
            options.MainDefaultWidth,
            options.ExtDefaultWidth,
            options.MainUseMaxWidth,
            (Node)globalUi);

        var clipHost = CreateBrowserPanelClipHost();
        clipHost.OffsetRight = -BrowserPanelRight;

        var mover = new Control {
            Name = options.CarrierNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        mover.AnchorLeft = 0.5f;
        mover.AnchorRight = 0.5f;
        mover.AnchorTop = 0.15f;
        mover.AnchorBottom = 0.85f;
        mover.OffsetTop = 0;
        mover.OffsetBottom = 0;

        var row = new HBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", 0);
        row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var mainSlot = new Control {
            CustomMinimumSize = new Vector2(mainW, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 1,
        };

        var mainPanel = CreateBrowserPanelInner(mainW);
        mainPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        mainSlot.AddChild(mainPanel);

        var mainContent = mainPanel.GetNodeOrNull<VBoxContainer>("Content")
            ?? throw new InvalidOperationException($"Dual column main panel missing Content ({options.RootName})");

        var extSlot = new Control {
            CustomMinimumSize = new Vector2(extW, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 0,
        };

        var extSlideHost = new Control { Name = "ExtSlideHost" };
        extSlideHost.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var extPanel = CreateBrowserPanelInner(extW);
        extPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        extSlideHost.AddChild(extPanel);
        extSlot.AddChild(extSlideHost);

        var extContent = extPanel.GetNodeOrNull<VBoxContainer>("Content")
            ?? throw new InvalidOperationException($"Dual column extension panel missing Content ({options.RootName})");

        row.AddChild(mainSlot);
        row.AddChild(extSlot);
        mover.AddChild(row);
        clipHost.AddChild(mover);
        root.AddChild(clipHost);

        var handle = new DualColumnOverlayHandle(
            options, root, clipHost, mover, mainPanel, mainContent, extContent, extPanel, extSlot,
            extSlideHost, mainW, extW);

        if (options.EnableDragHide)
            handle.WireDragAndHide(options.GlobalUi, options.HeaderTitle);

        return handle;
    }

    internal static DualColumnOverlayHandle CreateMainOnlyDualOverlay(
        NGlobalUi globalUi,
        string rootName,
        float mainDefaultWidth,
        Action fallbackClose,
        int zIndex = BrowserOverlayZIndex,
        int contentSeparation = 10,
        string? headerTitle = null) {
        var dual = CreateDualColumnOverlay(new DualColumnOverlayOptions {
            GlobalUi = globalUi,
            RootName = rootName,
            DualMetaKey = "dm_dual_" + rootName,
            CarrierNodeName = rootName + "DualCarrier",
            MainDefaultWidth = mainDefaultWidth,
            ExtDefaultWidth = 420f,
            FallbackClose = fallbackClose,
            ZIndex = zIndex,
            HeaderTitle = headerTitle,
        });
        dual.MainContent.AddThemeConstantOverride("separation", contentSeparation);
        return dual;
    }

    /// <summary>Headless host that drives <see cref="FloatingCombatOverlay.DraggablePanelBinding"/> every frame.</summary>
    private sealed class DragProcessHost : Control {
        internal FloatingCombatOverlay.DraggablePanelBinding? Binding;
        public DragProcessHost() {
            MouseFilter = Control.MouseFilterEnum.Ignore;
        }
        public override void _Process(double delta) => Binding?.Process();
    }

    /// <summary>Small flat icon button used in headers (collapse/hide).</summary>
    internal static Button CreateHeaderIconButton(MdiIcon icon, string tooltip) {
        var btn = new Button {
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(28, 28),
            Icon = icon.Texture(16, KitLibTheme.Subtle),
            TooltipText = tooltip,
        };
        var flat = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 2,
            ContentMarginBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };
        var hover = new StyleBoxFlat {
            BgColor = KitLibTheme.ButtonBgNormal,
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 2,
            ContentMarginBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };
        foreach (var st in new[] { "normal", "hover", "pressed", "focus" })
            btn.AddThemeStyleboxOverride(st, st == "normal" || st == "focus" ? flat : hover);
        btn.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        btn.AddThemeFontSizeOverride("font_size", 12);
        return btn;
    }
}
