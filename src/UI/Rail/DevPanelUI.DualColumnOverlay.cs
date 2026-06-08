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
        public required string MainWidthKey { get; init; }
        public required string ExtWidthKey { get; init; }
        public required Action FallbackClose { get; init; }
        public float MainDefaultWidth { get; init; } = 520f;
        public float ExtDefaultWidth { get; init; } = 420f;
        public float ExtSlideOutSec { get; init; } = 0.28f;
        public int ZIndex { get; init; } = 1250;
    }

    internal sealed class DualColumnOverlayHandle {
        private readonly DualColumnOverlayOptions _options;
        private readonly Control _mainSlot;
        private readonly Control _clipHost;
        private Tween? _extCloseTween;

        internal Control Root { get; }
        internal VBoxContainer MainContent { get; }
        internal VBoxContainer ExtContent { get; }
        internal PanelContainer MainPanel { get; }
        internal PanelContainer ExtPanel { get; }
        internal Control ExtSlot { get; }
        internal Control Mover { get; }

        internal DualColumnOverlayHandle(
            DualColumnOverlayOptions options,
            Control root,
            Control clipHost,
            Control mainSlot,
            Control mover,
            PanelContainer mainPanel,
            VBoxContainer mainContent,
            VBoxContainer extContent,
            PanelContainer extPanel,
            Control extSlot) {
            _options = options;
            Root = root;
            _clipHost = clipHost;
            _mainSlot = mainSlot;
            Mover = mover;
            MainPanel = mainPanel;
            MainContent = mainContent;
            ExtContent = extContent;
            ExtPanel = extPanel;
            ExtSlot = extSlot;
        }

        internal void AttachToScene() {
            _clipHost.Resized += SyncMoverWidth;

            bool opened = false;
            _clipHost.TreeEntered += () => {
                if (opened) return;
                opened = true;
                Callable.From(() => {
                    SyncMoverWidth();
                    PlaySubPanelSlideOpenFromLeft(Mover);
                }).CallDeferred();
            };

            ((Node)_options.GlobalUi).AddChild(Root);
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
            ExtPanel.Position = Vector2.Zero;
            ExtSlot.Visible = true;
            SyncMoverWidth();
        }

        internal void AnimateExtensionSlideIn() => PlayBrowserPanelOpenFromLeft(ExtPanel);

        internal void CloseExtension(Action? onHidden = null) {
            if (!ExtSlot.Visible) return;
            KillExtCloseTween();
            float w = Mathf.Max(1f, ExtPanel.GetRect().Size.X);
            _extCloseTween = ExtPanel.CreateTween();
            _extCloseTween.SetTrans(Tween.TransitionType.Cubic);
            _extCloseTween.SetEase(Tween.EaseType.In);
            _extCloseTween.TweenProperty(ExtPanel, "position:x", w, _options.ExtSlideOutSec);
            _extCloseTween.TweenCallback(Callable.From(() => {
                _extCloseTween = null;
                onHidden?.Invoke();
                ExtPanel.Position = Vector2.Zero;
                ExtSlot.Visible = false;
                SyncMoverWidth();
            }));
        }

        internal void KillExtCloseTween() {
            _extCloseTween?.Kill();
            _extCloseTween = null;
        }

        internal void SyncMoverWidth() {
            float totalW = _mainSlot.CustomMinimumSize.X + (ExtSlot.Visible ? ExtSlot.CustomMinimumSize.X : 0f);
            Mover.OffsetLeft = 0;
            Mover.OffsetRight = Mathf.Max(1f, totalW);
            SpliceBrowserPanelRight(MainPanel, ExtSlot.Visible);
        }
    }

    internal static DualColumnOverlayHandle CreateDualColumnOverlay(DualColumnOverlayOptions options) {
        var globalUi = options.GlobalUi;
        var root = CreateAndSetupRoot(globalUi, options.RootName, options.ZIndex);
        root.SetMeta(options.DualMetaKey, true);
        root.SetMeta(DualCarrierMetaKey, options.CarrierNodeName);

        root.AddChild(CreateBrowserBackdrop(
            () => RequestCloseBrowserOverlay(globalUi, options.RootName, options.FallbackClose)));

        float mainW = ResolveBrowserPanelWidth(options.MainWidthKey, options.MainDefaultWidth, (Node)globalUi);
        float extW = ResolveBrowserPanelWidth(options.ExtWidthKey, options.ExtDefaultWidth, (Node)globalUi);

        var clipHost = CreateBrowserPanelClipHost();

        var mover = new Control {
            Name = options.CarrierNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = true,
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
            ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        var extPanel = CreateBrowserPanelInner(extW);
        extPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        extSlot.AddChild(extPanel);

        var extContent = extPanel.GetNodeOrNull<VBoxContainer>("Content")
            ?? throw new InvalidOperationException($"Dual column extension panel missing Content ({options.RootName})");

        row.AddChild(mainSlot);
        row.AddChild(extSlot);
        mover.AddChild(row);
        clipHost.AddChild(mover);
        root.AddChild(clipHost);

        return new DualColumnOverlayHandle(
            options, root, clipHost, mainSlot, mover, mainPanel, mainContent, extContent, extPanel, extSlot);
    }
}
