using Godot;

namespace KitLib.UI;

/// <summary>
/// <para xml:lang="en">Top-edge notification tab ("dynamic island"): a black trapezoid hanging from the window's
/// top border, carrying one short message. Built onto <see cref="KitLibScreenFrame"/>'s own layer so it floats
/// above every KitLib panel, and painted with that frame's window-edge colors so the top edge merges with the
/// border it hangs from.</para>
/// <para xml:lang="zh-CN">顶部通知梯形（灵动岛式）：自窗口顶边垂下、承载一条短文案的黑色梯形。构建在
/// <see cref="KitLibScreenFrame"/> 自身图层上以浮于所有 KitLib 面板之上，并沿用该边框的窗口边配色，
/// 使顶边与所依附的边框无缝合并。</para>
/// </summary>
internal static class KitLibTopIslandChrome {
    internal const string ContentNodeName = "KitLibTopIslandContent";
    /// <summary><para xml:lang="en">Tab depth when fully revealed; deeper than the frame margin so it covers the
    /// inner stroke of the border it hangs from.</para>
    /// <para xml:lang="zh-CN">完全展开时的梯形深度；大于边框厚度，以盖住所依附边框的内描边。</para></summary>
    internal const float TabDepth = 64f;
    /// <summary><para xml:lang="en">Half-width of the tab's top edge, the widest part.</para>
    /// <para xml:lang="zh-CN">梯形顶边（最宽处）的半宽。</para></summary>
    internal const float TabHalfTop = 150f;
    /// <summary><para xml:lang="en">Half-width the slanted sides taper down to.</para>
    /// <para xml:lang="zh-CN">斜边向下收窄到的半宽。</para></summary>
    internal const float TabHalfBottom = 110f;
    /// <summary><para xml:lang="en">Radius of the outward bottom corners.</para>
    /// <para xml:lang="zh-CN">底部外扩圆角半径。</para></summary>
    internal const float CornerRadius = 18f;

    private const string RootNodeName = "KitLibTopIsland";
    private const string TabNodeName = "KitLibTopIslandTab";
    private const string IconDotNodeName = "KitLibTopIslandDot";
    private const string IconNodeName = "KitLibTopIslandIcon";
    private const string LabelNodeName = "KitLibTopIslandLabel";
    private const float IconSize = 22f;
    private const int ContentSeparation = 10;
    private const int LabelFontSize = 15;
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
        material.SetShaderParameter("tab_depth", 0f);
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
    /// <para xml:lang="en">Show one notification: message text, and either its icon or the accent dot.</para>
    /// <para xml:lang="zh-CN">展示一条通知：文案，以及其图标或主题色圆点。</para>
    /// </summary>
    internal static void SetContent(KitLibTopIsland island, KitLibNotification notification) {
        island.Label.Text = notification.Message;

        bool hasIcon = notification.Icon != null;
        island.Icon.Texture = notification.Icon;
        island.Icon.Visible = hasIcon;
        island.IconDot.Visible = !hasIcon;
    }

    /// <summary>
    /// <para xml:lang="en">Drive the reveal: 0 hides the tab (zero area), 1 shows it at full depth. The message
    /// fades in only once the tab is deep enough to hold the whole row, so it never floats outside the tab's
    /// outline while the tab is still unfolding.</para>
    /// <para xml:lang="zh-CN">驱动展开：0 为完全收起（面积为零），1 为全深度展开。文案只在梯形深到足以容纳整行后才淡入，
    /// 因此展开过程中不会飘到梯形轮廓之外。</para>
    /// </summary>
    internal static void SetPresence(KitLibTopIsland island, float presence) {
        island.Material.SetShaderParameter("tab_depth", TabDepth * presence);
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
            OffsetTop = KitLibScreenFrame.Margin,
            OffsetBottom = TabDepth - KitLibScreenFrame.Margin,
            Modulate = new Color(1f, 1f, 1f, 0f),
        };
        content.AddThemeConstantOverride("separation", ContentSeparation);
        island.Content = content;

        var dot = new Panel {
            Name = IconDotNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(IconSize, IconSize),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        dot.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = KitLibTheme.Accent,
            CornerRadiusTopLeft = (int)(IconSize * 0.5f),
            CornerRadiusTopRight = (int)(IconSize * 0.5f),
            CornerRadiusBottomLeft = (int)(IconSize * 0.5f),
            CornerRadiusBottomRight = (int)(IconSize * 0.5f),
        });
        content.AddChild(dot);
        island.IconDot = dot;

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
    // exactly at y = 0 with no stroke, so it merges with the border it overlaps, and the slanted / bottom
    // edges keep the frame's 1px inner stroke look. With Godot's fragment rules the shape is faded, not
    // returned from.
    private const string TopTabShaderCode = """
        shader_type canvas_item;

        uniform vec2 rect_size = vec2(1920.0, 1080.0);
        uniform float tab_center_x = 960.0;
        uniform float tab_half_top = 100.0;
        uniform float tab_half_bottom = 70.0;
        // Reveal depth from the top edge; 0 hides the tab (zero area).
        uniform float tab_depth = 0.0;
        uniform float tab_corner_radius = 14.0;
        uniform vec4 tab_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);
        uniform vec4 tab_edge_color : source_color = vec4(1.0, 1.0, 1.0, 0.10);
        uniform float tab_edge_width = 1.0;
        // Gutter the tab must stay inside on narrow windows: frame margin + corner radius.
        uniform float tab_inset = 32.0;

        void fragment() {
            vec2 p = UV * rect_size;
            float present = tab_depth > 0.0 ? 1.0 : 0.0;
            // Shrink to fit narrow windows so the tab never runs past the frame's rounded corners.
            float fit = min(1.0, max((rect_size.x - 2.0 * tab_inset) * 0.5, 1.0) / max(tab_half_top, 1.0));
            float half_top = tab_half_top * fit;
            float half_bottom = tab_half_bottom * fit;

            // Slanted sides run from the top edge down to where the corner arc takes over.
            float r = min(tab_corner_radius, tab_depth * 0.5);
            float slant_end = max(tab_depth - r, 0.0001);
            float half_w = mix(half_top, half_bottom, clamp(p.y / slant_end, 0.0, 1.0));
            if (p.y > slant_end) {
                // Bottom corners: a quarter arc bulging outward, so the tab ends on a rounded foot.
                float dy = p.y - slant_end;
                half_w = (half_bottom - r) + sqrt(max(r * r - dy * dy, 0.0));
            }

            // Inside distance like the frame's: sides and bottom form the silhouette, the top is open at y = 0.
            float d = min(half_w - abs(p.x - tab_center_x), tab_depth - p.y);

            vec3 col = tab_color.rgb;
            float a = tab_color.a * smoothstep(-0.5, 0.5, d);
            float edge = 1.0 - smoothstep(0.0, tab_edge_width, abs(d));
            col = mix(col, tab_edge_color.rgb, edge * tab_edge_color.a);
            a = max(a, edge * tab_edge_color.a);

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
    internal Panel IconDot = null!;
    internal Label Label = null!;
}
