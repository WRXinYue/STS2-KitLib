using Godot;

namespace KitLib.UI;

/// <summary>
/// <para xml:lang="en">Top notification tab: a black trapezoid hanging from the top border, carrying one short message.
/// Lives on <see cref="KitLibScreenFrame"/>'s layer so it floats above every KitLib panel.</para>
/// <para xml:lang="zh-CN">顶部通知梯形：自顶部边框垂下、承载一条短文案的黑色梯形。位于
/// <see cref="KitLibScreenFrame"/> 图层，浮于所有 KitLib 面板之上。</para>
/// </summary>
internal static class KitLibTopIslandChrome {
    internal const string ContentNodeName = "KitLibTopIslandContent";
    /// <summary><para xml:lang="en">Tab depth when fully revealed, measured down from the border's inner edge; the
    /// border itself is left intact instead of being painted over.</para>
    /// <para xml:lang="zh-CN">完全展开时的梯形深度，自边框内沿向下量起；边框本身保持完整，不被覆盖。</para></summary>
    internal const float TabDepth = 56f;
    /// <summary><para xml:lang="en">Half-width of the tab's top edge, the widest part.</para>
    /// <para xml:lang="zh-CN">梯形顶边（最宽处）的半宽。</para></summary>
    internal const float TabHalfTop = 200f;
    /// <summary><para xml:lang="en">Y of the tab's top edge; slightly inside the border.</para>
    /// <para xml:lang="zh-CN">梯形顶边的 y 坐标，略进入边框内部。</para></summary>
    internal const float TabTop = KitLibScreenFrame.Margin - 2f;
    /// <summary><para xml:lang="en">Half-width the slanted sides taper down to.</para>
    /// <para xml:lang="zh-CN">斜边向下收窄到的半宽。</para></summary>
    internal const float TabHalfBottom = 160f;
    /// <summary><para xml:lang="en">Fillet radius of the two bottom corners; the top edge stays flush with the
    /// border it hangs from.</para>
    /// <para xml:lang="zh-CN">底部两角的圆角半径；顶边保持与所依附边框齐平。</para></summary>
    internal const float CornerRadius = 10f;

    private const string RootNodeName = "KitLibTopIsland";
    private const string TabNodeName = "KitLibTopIslandTab";
    private const string IconNodeName = "KitLibTopIslandIcon";
    private const string LabelNodeName = "KitLibTopIslandLabel";
    private const float IconSize = 22f;
    private const int ContentSeparation = 10;
    private const int LabelFontSize = 12;
    /// <summary>Padding between the tab's outline and its content row.</summary>
    private const float ContentInset = 14f;
    /// <summary>Content half-width; inside <see cref="TabHalfBottom"/> so it never pokes past the tab's outline.</summary>
    private const float ContentHalfWidth = TabHalfBottom - 10f;
    /// <summary>Reveal progress at which the message starts / finishes fading in, once the tab is deep enough
    /// to hold the whole row.</summary>
    private const float ContentFadeFrom = 0.75f;
    private const float ContentFadeTo = 0.95f;
    /// <summary>Window gutter the tab must stay inside: frame margin plus the corner's own arc.</summary>
    private const float NarrowInset = KitLibScreenFrame.Margin + KitLibScreenFrame.Radius;

    private static Shader? _shader;

    /// <summary>
    /// <para xml:lang="en">Build one island under <paramref name="holder"/>; hidden until
    /// <see cref="SetPresence"/> reveals it.</para>
    /// <para xml:lang="zh-CN">在 <paramref name="holder"/> 下构建一个通知岛；在 <see cref="SetPresence"/> 展开前保持隐藏。</para>
    /// </summary>
    internal static KitLibTopIsland Build(Control? holder) {
        var island = new KitLibTopIsland();

        var material = new ShaderMaterial { Shader = TabShader() };
        material.SetShaderParameter("tab_half_top", TabHalfTop);
        material.SetShaderParameter("tab_half_bottom", TabHalfBottom);
        material.SetShaderParameter("tab_depth", TabDepth);
        material.SetShaderParameter("tab_top", TabTop * KitLibScreenFrame.WrapT);
        material.SetShaderParameter("tab_reveal", 0f);
        material.SetShaderParameter("tab_corner_radius", CornerRadius);
        material.SetShaderParameter("tab_edge_width", 1f);
        material.SetShaderParameter("tab_inset", NarrowInset);
        island.Material = material;

        var root = new Control {
            Name = RootNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        island.Root = root;

        var tab = new ColorRect {
            Name = TabNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Color = Colors.White,
            Material = material,
        };
        tab.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(tab);
        island.Tab = tab;

        BuildContent(island);

        SyncSize(island, ViewportSize(holder));
        holder?.AddChild(root);
        return island;
    }

    /// <summary>
    /// <para xml:lang="en">Re-map the tab to a new viewport size, keeping it centered and inside narrow windows.</para>
    /// <para xml:lang="zh-CN">按新的视口尺寸重映射梯形，保持居中并在窄窗口内收窄。</para>
    /// </summary>
    internal static void SyncSize(KitLibTopIsland island, Vector2 size) {
        island.Material.SetShaderParameter("rect_size", size);
        island.Material.SetShaderParameter("tab_center_x", size.X * 0.5f);
    }

    /// <summary>
    /// <para xml:lang="en">Show one notification: the message text, plus an optional leading icon.</para>
    /// <para xml:lang="zh-CN">展示一条通知：文案，以及可选的前置图标。</para>
    /// </summary>
    internal static void SetContent(KitLibTopIsland island, KitLibNotification notification) {
        island.Label.Text = notification.Message;

        island.Icon.Texture = notification.Icon;
        island.Icon.Visible = notification.Icon != null;
    }

    /// <summary>
    /// <para xml:lang="en">Drive the reveal: 0 hides the tab, 1 shows it at full depth. The whole trapezoid slides up
    /// into the border as it retracts instead of being squashed, so its outline moves as one piece. The message fades
    /// out over the top of the motion.</para>
    /// <para xml:lang="zh-CN">驱动展开：0 为完全收起，1 为全深度展开。收起时整块梯形上滑进边框，而不是被压扁，
    /// 因此轮廓始终作为一个整体移动；文案在动作前段淡出。</para>
    /// </summary>
    internal static void SetPresence(KitLibTopIsland island, float presence) {
        island.Material.SetShaderParameter("tab_reveal", presence);
        // Ride the border's inner edge as the wrap retracts, otherwise the border pulls away and leaves a gap.
        island.Material.SetShaderParameter("tab_top", TabTop * KitLibScreenFrame.WrapT);
        // Smooth Hermite fade over the top of the reveal.
        float t = Mathf.Clamp((presence - ContentFadeFrom) / (ContentFadeTo - ContentFadeFrom), 0f, 1f);
        island.Content.Modulate = new Color(1f, 1f, 1f, t * t * (3f - 2f * t));
    }

    private static void BuildContent(KitLibTopIsland island) {
        var content = new HBoxContainer {
            Name = ContentNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = true,
            Alignment = BoxContainer.AlignmentMode.Center,
            // Anchored top-center, spanning the tab's widest span: the row stays clear of the tapering sides.
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetLeft = -ContentHalfWidth,
            OffsetRight = ContentHalfWidth,
            OffsetTop = TabTop + ContentInset,
            OffsetBottom = TabTop + TabDepth - ContentInset,
            Modulate = new Color(1f, 1f, 1f, 0f),
        };
        content.AddThemeConstantOverride("separation", ContentSeparation);
        island.Content = content;

        var icon = new TextureRect {
            Name = IconNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(IconSize, IconSize),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        content.AddChild(icon);
        island.Icon = icon;

        var label = new Label {
            Name = LabelNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            // ClipText keeps the text out of the row's minimum size, so a long message can never
            // stretch the row past the tab's outline; it trims with an ellipsis instead.
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        };
        KitLibLocaleFonts.ApplyControl(label);
        label.AddThemeFontSizeOverride("font_size", LabelFontSize);
        label.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        content.AddChild(label);
        island.Label = label;

        island.Root.AddChild(content);
    }

    private static Vector2 ViewportSize(Node? holder) =>
        holder?.GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920f, 1080f);

    private static Shader TabShader() {
        if (_shader != null && GodotObject.IsInstanceValid(_shader))
            return _shader;

        _shader = new Shader { Code = TopTabShaderCode };
        return _shader;
    }

    // Trapezoid hanging from the window's top edge, in logical pixels (UV * rect_size). The top edge sits
    // exactly at y = 0 with no stroke, so it merges with the border it overlaps; the slanted sides, the
    // rounded bottom corners and the bottom edge keep the frame's 1px inner stroke look. With Godot's
    // fragment rules the shape is faded, not returned from.
    private const string TopTabShaderCode = """
        shader_type canvas_item;

        uniform vec2 rect_size = vec2(1920.0, 1080.0);
        uniform float tab_center_x = 960.0;
        uniform float tab_half_top = 100.0;
        uniform float tab_half_bottom = 70.0;
        // Full depth below the border; the shape itself never changes size.
        uniform float tab_depth = 56.0;
        // Y of the tab's top edge, slightly inside the border.
        uniform float tab_top = 14.0;
        // Reveal progress 0..1: the whole tab slides up into the border as it retracts.
        uniform float tab_reveal = 0.0;
        uniform vec4 tab_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);
        uniform vec4 tab_edge_color : source_color = vec4(1.0, 1.0, 1.0, 0.10);
        uniform float tab_edge_width = 1.0;
        // Fillet radius of the two bottom corners only; the top edge stays straight and flush.
        uniform float tab_corner_radius = 10.0;
        // Gutter the tab must stay inside on narrow windows: frame margin + corner radius.
        uniform float tab_inset = 32.0;

        void fragment() {
            vec2 p = UV * rect_size;
            float revealed = clamp(tab_reveal, 0.0, 1.0);
            float present = revealed > 0.0 ? 1.0 : 0.0;
            // Shift the whole shape up as it retracts; its geometry never deforms. y is the shape's own space.
            float y = p.y - tab_top + (1.0 - revealed) * tab_depth;
            // Shrink to fit narrow windows so the tab never runs past the frame's rounded corners.
            float fit = min(1.0, max((rect_size.x - 2.0 * tab_inset) * 0.5, 1.0) / max(tab_half_top, 1.0));
            float half_top = tab_half_top * fit;
            float half_bottom = tab_half_bottom * fit;

            // Straight slanted sides running the full depth: a plain trapezoid, no bulging feet.
            float half_w = mix(half_top, half_bottom, clamp(y / max(tab_depth, 0.0001), 0.0, 1.0));

            // Inside distances to the silhouette: the slanted sides and the bottom edge. The top is open so the
            // tab reads as one solid piece with the border it hangs from.
            float d_side = half_w - abs(p.x - tab_center_x);
            float d_bottom = tab_depth - y;

            // Round the bottom corners by treating (side, bottom) as the two axes of a box SDF: the fillet
            // only bites where both distances fall inside the radius, leaving the top edge and the sides
            // themselves untouched.
            float r = min(tab_corner_radius, tab_depth * 0.5);
            vec2 q = vec2(r - d_side, r - d_bottom);
            float d = r - length(max(q, vec2(0.0))) - min(max(q.x, q.y), 0.0);

            vec3 col = tab_color.rgb;
            float a = tab_color.a * smoothstep(-0.5, 0.5, d);
            float edge = 1.0 - smoothstep(0.0, tab_edge_width, abs(d));
            col = mix(col, tab_edge_color.rgb, edge * tab_edge_color.a);
            a = max(a, edge * tab_edge_color.a);
            // Cut at the shape's own top edge and again at the border line, so a retracting tab disappears into
            // the border instead of surfacing above it.
            a *= smoothstep(-0.5, 0.5, y) * smoothstep(-0.5, 0.5, p.y - tab_top);

            COLOR = vec4(col, a * present);
        }
        """;
}

/// <summary>
/// <para xml:lang="en">Live node handles of one top island, as returned by <see cref="KitLibTopIslandChrome.Build"/>.</para>
/// <para xml:lang="zh-CN">一个顶部通知岛的节点句柄，由 <see cref="KitLibTopIslandChrome.Build"/> 返回。</para>
/// </summary>
internal sealed class KitLibTopIsland {
    internal Control Root = null!;
    internal ColorRect Tab = null!;
    internal ShaderMaterial Material = null!;
    internal HBoxContainer Content = null!;
    internal TextureRect Icon = null!;
    internal Label Label = null!;
}
