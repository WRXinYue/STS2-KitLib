using System;
using Godot;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    internal const string SectionCardMeta = "_dm_section_card";

    internal sealed class SplitBodyOptions {
        public string Name { get; init; } = "body.split";
        public int InitialSplitRight { get; init; } = 280;
        public int MinSplitRight { get; init; } = 220;
        public int MinMainWidth { get; init; } = 320;
        public int InnerSeparation { get; init; } = 8;
    }

    internal readonly record struct SplitBodyHandle(
        HSplitContainer Split,
        ScrollContainer MainScroll,
        VBoxContainer MainInner,
        PanelContainer SidebarPanel);

    /// <summary>Main scroll area + optional permanent right sidebar slot.</summary>
    internal static SplitBodyHandle CreateSplitBody(SplitBodyOptions? options = null) {
        options ??= new SplitBodyOptions();

        var scroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var inner = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        inner.AddThemeConstantOverride("separation", options.InnerSeparation);
        scroll.AddChild(inner);

        var split = new HSplitContainer {
            Name = options.Name,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        split.AddChild(scroll);

        var sidebarPanel = new PanelContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(options.MinSplitRight, 0),
        };
        ApplySidebarPanelStyle(sidebarPanel, mergeLeft: false);
        split.AddChild(sidebarPanel);

        return new SplitBodyHandle(split, scroll, inner, sidebarPanel);
    }

    internal static void ApplySplitOffset(SplitBodyHandle handle, SplitBodyOptions options) {
        if (!GodotObject.IsInstanceValid(handle.Split))
            return;
        float total = handle.Split.Size.X;
        if (total < options.MinSplitRight + options.MinMainWidth)
            return;

        int right = Math.Clamp(
            options.InitialSplitRight,
            options.MinSplitRight,
            (int)total - options.MinMainWidth);
        int left = Math.Clamp((int)total - right, options.MinMainWidth, (int)total - options.MinSplitRight);
        handle.Split.SplitOffset = left;
    }

    internal static Action AttachMergeStyle(SplitBodyHandle handle) {
        void Update() => UpdateMergeStyle(handle);
        handle.MainScroll.Resized += Update;
        handle.MainInner.Resized += Update;
        return () => {
            if (GodotObject.IsInstanceValid(handle.MainScroll))
                handle.MainScroll.Resized -= Update;
            if (GodotObject.IsInstanceValid(handle.MainInner))
                handle.MainInner.Resized -= Update;
        };
    }

    internal static Action AttachSplitInit(SplitBodyHandle handle, SplitBodyOptions options) {
        void Init() => ApplySplitOffset(handle, options);
        handle.Split.Resized += Init;
        if (handle.Split.IsInsideTree())
            Callable.From(Init).CallDeferred();
        else
            handle.Split.TreeEntered += () => Callable.From(Init).CallDeferred();
        return () => {
            if (GodotObject.IsInstanceValid(handle.Split))
                handle.Split.Resized -= Init;
        };
    }

    internal static Control MakeSectionCard(string title, Action<VBoxContainer> fillBody) {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.SetMeta(SectionCardMeta, true);
        ApplySectionCardStyle(panel, mergeRight: false);

        var outer = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        outer.AddThemeConstantOverride("separation", 8);

        var head = new Label { Text = title };
        head.AddThemeFontSizeOverride("font_size", 13);
        head.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        outer.AddChild(head);

        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 6);
        fillBody(body);
        outer.AddChild(body);

        panel.AddChild(outer);
        return panel;
    }

    internal static void UpdateMergeStyle(SplitBodyHandle handle) {
        if (!GodotObject.IsInstanceValid(handle.MainScroll) || !GodotObject.IsInstanceValid(handle.MainInner))
            return;
        bool merge = handle.MainInner.Size.Y >= handle.MainScroll.Size.Y - 2f;
        foreach (var node in handle.MainInner.FindChildren("*", recursive: true, owned: false)) {
            if (node is PanelContainer pc && pc.HasMeta(SectionCardMeta))
                ApplySectionCardStyle(pc, merge);
        }
        if (GodotObject.IsInstanceValid(handle.SidebarPanel))
            ApplySidebarPanelStyle(handle.SidebarPanel, merge);
    }

    internal static void ApplySectionCardStyle(PanelContainer panel, bool mergeRight) {
        var style = new StyleBoxFlat {
            BgColor = new Color(KitLibTheme.PanelBg.R, KitLibTheme.PanelBg.G, KitLibTheme.PanelBg.B, 0.55f),
            BorderColor = KitLibTheme.PanelBorder,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthRight = mergeRight ? 0 : 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusTopRight = mergeRight ? 0 : 8,
            CornerRadiusBottomRight = mergeRight ? 0 : 8,
            ContentMarginLeft = 14,
            ContentMarginRight = mergeRight ? 10 : 14,
            ContentMarginTop = 12,
            ContentMarginBottom = 14,
        };
        panel.AddThemeStyleboxOverride("panel", style);
    }

    internal static void ApplySidebarPanelStyle(PanelContainer panel, bool mergeLeft) {
        var style = new StyleBoxFlat {
            BgColor = new Color(KitLibTheme.PanelBg.R, KitLibTheme.PanelBg.G, KitLibTheme.PanelBg.B, 0.55f),
            BorderColor = KitLibTheme.PanelBorder,
            BorderWidthLeft = mergeLeft ? 0 : 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = mergeLeft ? 0 : 8,
            CornerRadiusBottomLeft = mergeLeft ? 0 : 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = mergeLeft ? 10 : 14,
            ContentMarginRight = 14,
            ContentMarginTop = 12,
            ContentMarginBottom = 14,
        };
        panel.AddThemeStyleboxOverride("panel", style);
    }
}
