using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    internal const string DualCarrierMetaKey = "dm_dual_carrier_name";

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
        /// <summary>Invoked once after the open slide finishes (or immediately if the slide is skipped).</summary>
        public Action? OnOpenAnimationFinished { get; init; }
    }

    internal sealed class DualColumnOverlayHandle {
        private readonly DualColumnOverlayOptions _options;
        private readonly Control _clipHost;
        private readonly float _nominalMainW;
        private readonly float _nominalExtW;
        private Tween? _extCloseTween;

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
            Mover.OffsetLeft = 0;
            Mover.OffsetRight = Mathf.Max(1f, totalW);
            bool joined = ExtSlot.Visible;
            SpliceBrowserPanelRight(MainPanel, joined);
            SpliceBrowserPanelLeft(ExtPanel, joined);
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
        mover.AnchorLeft = 0;
        mover.AnchorRight = 0;
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

        var mainPanel = CreateBrowserPanelInner(mainW, joinFlushOnRight: false);
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

        return new DualColumnOverlayHandle(
            options, root, clipHost, mover, mainPanel, mainContent, extContent, extPanel, extSlot,
            extSlideHost, mainW, extW);
    }

    internal static DualColumnOverlayHandle CreateMainOnlyDualOverlay(
        NGlobalUi globalUi,
        string rootName,
        float mainDefaultWidth,
        Action fallbackClose,
        int zIndex = BrowserOverlayZIndex,
        int contentSeparation = 10) {
        var dual = CreateDualColumnOverlay(new DualColumnOverlayOptions {
            GlobalUi = globalUi,
            RootName = rootName,
            DualMetaKey = "dm_dual_" + rootName,
            CarrierNodeName = rootName + "DualCarrier",
            MainDefaultWidth = mainDefaultWidth,
            ExtDefaultWidth = 420f,
            FallbackClose = fallbackClose,
            ZIndex = zIndex,
        });
        dual.MainContent.AddThemeConstantOverride("separation", contentSeparation);
        return dual;
    }
}
