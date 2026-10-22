using System;
using System.Collections.Generic;
using Godot;
using KitLib.Abstractions.Host;
using KitLib.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

/// <summary>
/// Full-screen rounded frame wrapping the game window on all four sides, so the game reads as a
/// phone-app-style window floating over the wallpaper: opaque black outside a rounded rect, a 1px
/// light stroke hugging the inside of the boundary, fully transparent inside.
/// </summary>
/// <remarks>
/// Purely decorative and input-transparent. Chrome that would land under the frame is pushed inward
/// by <see cref="Margin"/> — the in-run TopBar and hand container through the rail inset, the
/// main-menu modded warning, version text and patch-notes shortcut through <see cref="ApplyMainMenuInsets"/>. The left side reads as a wider
/// band: <see cref="CreateLeftBand"/> draws the same shape from the rail's own layer with only the
/// left thickness widened, and <see cref="SetLeftBand"/> cuts this layer's inner stroke there, so
/// both layers together read as one window edge with a single continuous inner outline. A band can be
/// stopped short with a step so the narrow frame edge takes over below it. The bottom-right stripe
/// also carries KitLib's own version stamp — see <see cref="EnsureStamp"/>.
/// </remarks>
internal static class KitLibScreenFrame {
    internal const string RootNodeName = "KitLibScreenFrame";
    /// <summary>Node name of a rail's widened left contour band (see <see cref="CreateLeftBand"/>).</summary>
    internal const string LeftBandNodeName = "RailBackdrop";
    /// <summary>Thickness of the frame on every side, in logical pixels.</summary>
    internal const float Margin = 16f;
    /// <summary>Corner radius of the wrapped window, in logical pixels.</summary>
    internal const int Radius = 16;

    // Max CanvasLayer.layer: above every layer-0 game node and KitLib rail/overlay.
    private const int LayerId = 128;
    private const float EdgeWidth = 1f;
    /// <summary>Duration of a retract / restore transition, in seconds.</summary>
    internal const float AnimationSeconds = 0.22f;
    /// <summary>Frame fill; the widened left contour bands reuse it so all read as one window edge.</summary>
    internal static readonly Color FrameColor = new(0f, 0f, 0f, 1f);
    private static readonly Color EdgeColor = new(1f, 1f, 1f, 0.10f);

    /// <summary>Node name of the KitLib version stamp drawn on the border's bottom-right stripe.</summary>
    private const string StampNodeName = "KitLibVersionStamp";
    private const string StampFontPath = "res://themes/kreon_regular_shared.tres";
    private const int StampFontSize = 10;
    private static readonly Color StampColor = new(1f, 1f, 1f, 0.45f);

    private static Label? _stamp;
    private static string _stampText = "";

    private static CanvasLayer? _layer;
    private static ColorRect? _rect;
    private static Viewport? _boundViewport;
    private static Shader? _shader;
    private static ShaderMaterial? _material;
    private static float _leftBandWidth;
    // Bands run the frame shader at a rail's own width; tracked so viewport resizes re-map them.
    private static readonly List<(ColorRect Node, ShaderMaterial Material)> _bands = new();

    // Optional top notification island drawn on this layer, so it floats above every KitLib panel.
    private static KitLibTopIsland? _topIsland;

    // ── Wrap presence ──
    // 1 = full window edge on screen, 0 = the whole wrap (four edges + rail + insets) retracted.
    // Rail hosts drive it (Ctrl+B) and read it back to slide their rail and scale their inset by the
    // same progress, so frame, contour and pushed chrome move as one.
    private static float _wrapT = 1f;
    private static bool _wrapShown = true;
    private static Tween? _wrapTween;
    private static readonly List<Action> _wrapListeners = new();

    // ── Main-menu chrome pushed clear of the frame ──
    private static NMainMenu? _menuOwner;
    private static readonly List<MenuEntry> _menuPushed = new();

    /// <summary>Attach the frame layer to the scene tree root; idempotent.</summary>
    internal static void EnsureAttached() {
        try {
            if (_layer != null && GodotObject.IsInstanceValid(_layer) && _layer.IsInsideTree())
                return;

            if (Engine.GetMainLoop() is not SceneTree tree ||
                tree.Root == null || !GodotObject.IsInstanceValid(tree.Root))
                return;

            UnbindViewport();

            var existing = tree.Root.GetNodeOrNull<CanvasLayer>(RootNodeName);
            if (existing != null && GodotObject.IsInstanceValid(existing)) {
                _layer = existing;
                _rect = _layer.GetNodeOrNull<ColorRect>("FrameHolder/FrameRect");
                EnsureStamp(_layer.GetNodeOrNull<Control>("FrameHolder"));
            }
            else {
                _layer = new CanvasLayer {
                    Name = RootNodeName,
                    Layer = LayerId,
                };
                tree.Root.AddChild(_layer);

                var holder = new Control {
                    Name = "FrameHolder",
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                holder.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                _layer.AddChild(holder);

                _rect = new ColorRect {
                    Name = "FrameRect",
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Color = Colors.White,
                    Material = FrameMaterial(),
                };
                _rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                holder.AddChild(_rect);
                EnsureStamp(holder);
            }

            BindViewport();
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib screen frame attach failed: {ex.Message}");
        }
    }

    /// <summary>Current wrap progress: 1 = fully on screen, 0 = fully retracted.</summary>
    internal static float WrapT => _wrapT;

    /// <summary>Whether the wrap is currently targeted on screen (animations may still be running).</summary>
    internal static bool WrapShown => _wrapShown;

    /// <summary>True while a rail host is driving the wrap; Ctrl+B is meaningless without one.</summary>
    internal static bool HasWrapHost => _wrapListeners.Count > 0;

    /// <summary>Retract the wrap when shown, restore it when retracted.</summary>
    internal static void ToggleWrapShown(bool animated) => SetWrapShown(!_wrapShown, animated);

    /// <summary>
    /// Retract or restore the whole wrap: the four window edges, the rail host's widened left contour
    /// plus its rail, and the chrome those push aside. Listeners are pinged on every step so hosts can
    /// move their own nodes by the same progress.
    /// </summary>
    internal static void SetWrapShown(bool shown, bool animated) {
        float target = shown ? 1f : 0f;
        _wrapShown = shown;

        if (_wrapTween != null && GodotObject.IsInstanceValid(_wrapTween))
            _wrapTween.Kill();
        _wrapTween = null;

        if (Mathf.IsEqualApprox(_wrapT, target))
            return;

        if (!animated || _rect == null || !GodotObject.IsInstanceValid(_rect)) {
            SetWrapProgress(target);
            return;
        }

        var tween = _rect.CreateTween();
        tween.TweenMethod(Callable.From<float>(SetWrapProgress), _wrapT, target, AnimationSeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        // The tween's last step can land short of the target; snap so hosts settle at a clean state.
        tween.Finished += () => SetWrapProgress(target);
        _wrapTween = tween;
    }

    /// <summary>Register a host callback invoked on every wrap step (rail placement, band, insets).</summary>
    internal static void AddWrapListener(Action listener) {
        if (!_wrapListeners.Contains(listener))
            _wrapListeners.Add(listener);
    }

    internal static void RemoveWrapListener(Action listener) => _wrapListeners.Remove(listener);

    /// <summary>
    /// KitLib's version stamp on the border's bottom-right stripe. The border sits outside every game
    /// node, so the stamp can live there without ever covering the hand or the card piles.
    /// </summary>
    private static void EnsureStamp(Control? holder) {
        if (_stamp == null || !GodotObject.IsInstanceValid(_stamp)) {
            if (holder == null || !GodotObject.IsInstanceValid(holder))
                return;

            _stamp = new Label {
                Name = StampNodeName,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                AnchorLeft = 1f,
                AnchorTop = 1f,
                AnchorRight = 1f,
                AnchorBottom = 1f,
                // Right-aligned inside the bottom stripe, stopping short of the corner's own arc.
                OffsetLeft = -152f,
                OffsetRight = -(Radius + 16f),
                OffsetTop = -Margin,
                OffsetBottom = 0f,
            };
            if (ResourceLoader.Exists(StampFontPath))
                _stamp.AddThemeFontOverride("font", GD.Load<Font>(StampFontPath));
            _stamp.AddThemeFontSizeOverride("font_size", StampFontSize);
            _stamp.AddThemeColorOverride("font_color", StampColor);
            holder.AddChild(_stamp);
        }

        // The catalog only fills in once the game's own mod scan finishes, so keep asking until it does.
        if (string.IsNullOrEmpty(_stampText))
            _stampText = ResolveStampText();
        if (!string.Equals(_stamp.Text, _stampText, StringComparison.Ordinal))
            _stamp.Text = _stampText;

        SyncStampPresence();
    }

    /// <summary>KitLib's own version from the mod catalog, or empty while the catalog is still filling in.</summary>
    private static string ResolveStampText() {
        try {
            foreach (var info in ModRuntime.GetOrderedLoadedMods()) {
                if (string.Equals(info.Id, KitLibProductIds.KitLib, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(info.Version))
                    return $"KitLib v{info.Version}";
            }
        }
        catch (Exception) {
            // Catalog is optional until mods finish loading.
        }

        return "";
    }

    /// <summary>Fade the stamp with the wrap so it retracts along with the border it is printed on.</summary>
    private static void SyncStampPresence() {
        if (_stamp == null || !GodotObject.IsInstanceValid(_stamp))
            return;

        _stamp.Visible = _wrapT > 0.01f;
        _stamp.Modulate = new Color(1f, 1f, 1f, _wrapT);
    }

    private static void SetWrapProgress(float t) {
        _wrapT = t;
        ApplyWrap();
    }

    private static void ApplyWrap() {
        var material = FrameMaterial();
        material.SetShaderParameter("margin", new Vector4(Margin * _wrapT, Margin * _wrapT,
            Margin * _wrapT, Margin * _wrapT));
        material.SetShaderParameter("radius", (float)Radius * _wrapT);
        SyncStampPresence();

        for (int i = _wrapListeners.Count - 1; i >= 0; i--) {
            var listener = _wrapListeners[i];
            if (listener == null)
                _wrapListeners.RemoveAt(i);
            else
                listener();
        }
    }

    /// <summary>
    /// Push main-menu chrome that would sit under the frame back inside it. Records the vanilla
    /// offsets so a rebuilt menu re-bases instead of accumulating.
    /// </summary>
    internal static void ApplyMainMenuInsets(NMainMenu menu) {
        if (menu == null || !GodotObject.IsInstanceValid(menu))
            return;

        if (_menuOwner != menu) {
            // The previous menu and its nodes are gone; drop the stale records.
            _menuPushed.Clear();
            _menuOwner = menu;
        }

        // Chrome the frame crowds: the bottom-right modded warning reaches under it, and the top-right
        // version text plus patch-notes shortcut sit only a couple of pixels clear of it. The two share
        // one visual row, so they move by the same amount to stay aligned.
        PushInward(menu.GetNodeOrNull<Control>("%ModdedWarning"));
        PushInward(menu.GetNodeOrNull<Control>("%ReleaseInfo"));
        PushInward(menu.GetNodeOrNull<Control>("%PatchNotesButton"));

        // Main-menu syncs outlive the mod scan, so this is where the stamp picks up KitLib's version.
        EnsureStamp(null);
    }

    /// <summary>
    /// Move one chrome node <see cref="Margin"/> px away from the window edge it is anchored to, scaled
    /// by the wrap progress so it rides back out as the frame retracts. Free edges follow the anchored
    /// one so the node keeps its size.
    /// </summary>
    private static void PushInward(Control? node) {
        if (node == null || !GodotObject.IsInstanceValid(node))
            return;

        int index = _menuPushed.FindIndex(e => e.Node == node);
        if (index < 0) {
            _menuPushed.Add(new MenuEntry(node, node.OffsetLeft, node.OffsetTop,
                node.OffsetRight, node.OffsetBottom));
            index = _menuPushed.Count - 1;
        }

        var entry = _menuPushed[index];
        // The game owns the offset when it differs from what we last wrote -> re-capture the base.
        bool gameOwned = !Mathf.IsEqualApprox(node.OffsetLeft, entry.LastLeft) ||
                         !Mathf.IsEqualApprox(node.OffsetTop, entry.LastTop);
        if (gameOwned) {
            entry.Left = node.OffsetLeft;
            entry.Top = node.OffsetTop;
            entry.Right = node.OffsetRight;
            entry.Bottom = node.OffsetBottom;
        }

        float push = Margin * _wrapT;
        float dx = node.AnchorLeft > 0.5f ? -push : push;
        float dy = node.AnchorTop > 0.5f ? -push : push;

        node.OffsetLeft = entry.Left + dx;
        node.OffsetTop = entry.Top + dy;
        node.OffsetRight = entry.Right + dx;
        node.OffsetBottom = entry.Bottom + dy;

        entry.LastLeft = node.OffsetLeft;
        entry.LastTop = node.OffsetTop;
        _menuPushed[index] = entry;
    }

    /// <summary>
    /// Report a rail's widened left contour state: <paramref name="band"/> is the rect returned by
    /// <see cref="CreateLeftBand"/> (null when the caller has none) and <paramref name="width"/> is the
    /// width it currently occupies — already scaled by the caller's own presence, so 0 means retracted.
    /// <paramref name="stepY"/> (&gt; 0) ends the widened strip there, bending the band back onto this
    /// layer's own narrow outline over <paramref name="stepCurve"/> px of arc.
    /// Rewrites this layer's stroke cut and the band's own geometry, keeping both in step with the wrap.
    /// </summary>
    internal static void SetLeftBand(ColorRect? band, float width, float stepY = -1f, float stepCurve = 0f) {
        if (!Mathf.IsEqualApprox(_leftBandWidth, width)) {
            _leftBandWidth = width;
            FrameMaterial().SetShaderParameter("stroke_cut_x", BandStrokeCut());
        }
        FrameMaterial().SetShaderParameter("stroke_cut_end_y", stepY);

        if (band == null || !GodotObject.IsInstanceValid(band) || band.Material is not ShaderMaterial material)
            return;

        // The band's own edges scale with the wrap so both layers stay one continuous window edge.
        float side = Margin * _wrapT;
        material.SetShaderParameter("margin", new Vector4(width, side, side, side));
        material.SetShaderParameter("radius", (float)Radius * _wrapT);
        material.SetShaderParameter("drop_below_y", stepY);
        material.SetShaderParameter("drop_below_curve", stepCurve);
    }

    /// <summary>Retract the widened left contour without touching any band's geometry.</summary>
    internal static void SetLeftBand(float width) => SetLeftBand(null, width);

    /// <summary>
    /// <para xml:lang="en">Suppress this layer's own inner stroke along the right edge between the two y's, so a band drawn in
    /// the frame colour continues the boundary unbroken instead of leaving a seam. Pass <c>y1 &lt;= 0</c> to draw it whole again.</para>
    /// <para xml:lang="zh-CN">在给定高度区间内抑制本层右边框的内描边，使同色竖带与边框连成一整块、不留缝。传 <c>y1 &lt;= 0</c> 恢复整条描边。</para>
    /// </summary>
    internal static void SetRightStrokeCut(float y0, float y1) {
        var material = FrameMaterial();
        material.SetShaderParameter("stroke_cut_y0", y0);
        material.SetShaderParameter("stroke_cut_y1", y1);
    }

    /// <summary>
    /// Widened left contour band for a rail: a full-screen rect running the frame shader with only the
    /// left thickness widened to <paramref name="width"/>, so its fill continues this frame and its
    /// inner boundary (straight edge + fillets into the top/bottom edges) is the window's left edge.
    /// Rail code toggles its visibility with the rail and reports the same width to
    /// <see cref="SetLeftBand"/>.
    /// </summary>
    internal static ColorRect CreateLeftBand(float width) {
        PruneBands();

        float side = Margin * _wrapT;
        var material = new ShaderMaterial { Shader = FrameShader() };
        material.SetShaderParameter("rect_size", ViewportSize());
        material.SetShaderParameter("margin", new Vector4(width, side, side, side));
        material.SetShaderParameter("radius", (float)Radius * _wrapT);
        material.SetShaderParameter("edge_width", EdgeWidth);
        material.SetShaderParameter("frame_color", FrameColor);
        material.SetShaderParameter("edge_color", EdgeColor);
        // The band owns its inner outline all the way round: nothing cuts it.
        material.SetShaderParameter("stroke_cut_x", -1f);
        // Full height until a caller reports a step.
        material.SetShaderParameter("drop_below_y", -1f);

        var band = new ColorRect {
            Name = LeftBandNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Color = Colors.White,
            Material = material,
        };
        band.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _bands.Add((band, material));
        return band;
    }

    private static float BandStrokeCut() => _leftBandWidth > 0f ? _leftBandWidth + Radius : -1f;

    /// <summary>
    /// Create the top notification island on this layer, painted with the frame's own window-edge colors so it
    /// reads as part of the same border. The caller owns the returned handle and frees it with
    /// <see cref="RemoveTopIsland"/>.
    /// </summary>
    internal static KitLibTopIsland CreateTopIsland() {
        EnsureAttached();

        var holder = _layer != null && GodotObject.IsInstanceValid(_layer)
            ? _layer.GetNodeOrNull<Control>("FrameHolder")
            : null;
        var island = KitLibTopIslandChrome.Build(holder);
        island.Material.SetShaderParameter("tab_color", FrameColor);
        island.Material.SetShaderParameter("tab_edge_color", EdgeColor);
        _topIsland = island;
        return island;
    }

    /// <summary>Free an island previously returned by <see cref="CreateTopIsland"/>.</summary>
    internal static void RemoveTopIsland(KitLibTopIsland island) {
        if (_topIsland == island)
            _topIsland = null;

        if (island.Root == null || !GodotObject.IsInstanceValid(island.Root))
            return;

        island.Root.GetParent()?.RemoveChild(island.Root);
        island.Root.QueueFree();
    }

    private static Vector2 ViewportSize() =>
        _rect?.GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920f, 1080f);

    private static void PruneBands() {
        for (int i = _bands.Count - 1; i >= 0; i--) {
            if (_bands[i].Node == null || !GodotObject.IsInstanceValid(_bands[i].Node))
                _bands.RemoveAt(i);
        }
    }

    private static void BindViewport() {
        var viewport = _rect?.GetViewport();
        if (viewport == null)
            return;

        _boundViewport = viewport;
        viewport.SizeChanged += SyncRectSize;
        SyncRectSize();
    }

    private static void UnbindViewport() {
        if (_boundViewport != null && GodotObject.IsInstanceValid(_boundViewport))
            _boundViewport.SizeChanged -= SyncRectSize;
        _boundViewport = null;
    }

    private static void SyncRectSize() {
        var viewport = _rect?.GetViewport();
        if (viewport == null)
            return;

        Vector2 size = viewport.GetVisibleRect().Size;
        FrameMaterial().SetShaderParameter("rect_size", size);
        PruneBands();
        foreach (var (_, material) in _bands)
            material.SetShaderParameter("rect_size", size);

        if (_topIsland != null && GodotObject.IsInstanceValid(_topIsland.Material))
            KitLibTopIslandChrome.SyncSize(_topIsland, size);
    }

    private static ShaderMaterial FrameMaterial() {
        if (_material != null && GodotObject.IsInstanceValid(_material))
            return _material;

        _material = new ShaderMaterial { Shader = FrameShader() };
        _material.SetShaderParameter("rect_size", ViewportSize());
        _material.SetShaderParameter("margin", new Vector4(Margin, Margin, Margin, Margin));
        _material.SetShaderParameter("radius", (float)Radius);
        _material.SetShaderParameter("edge_width", EdgeWidth);
        _material.SetShaderParameter("frame_color", FrameColor);
        _material.SetShaderParameter("edge_color", EdgeColor);
        _material.SetShaderParameter("stroke_cut_x", BandStrokeCut());
        _material.SetShaderParameter("stroke_cut_end_y", -1f);
        _material.SetShaderParameter("stroke_cut_y0", -1f);
        _material.SetShaderParameter("stroke_cut_y1", -1f);
        return _material;
    }

    private static Shader FrameShader() {
        if (_shader != null && GodotObject.IsInstanceValid(_shader))
            return _shader;

        _shader = new Shader { Code = FrameShaderCode };
        return _shader;
    }

    /// <summary>Mutable record of a pushed node and its vanilla offsets.</summary>
    private struct MenuEntry {
        public Control Node;
        public float Left;
        public float Top;
        public float Right;
        public float Bottom;
        public float LastLeft;
        public float LastTop;

        public MenuEntry(Control node, float left, float top, float right, float bottom) {
            Node = node;
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
            LastLeft = float.NaN;
            LastTop = float.NaN;
        }
    }

    // Rounded-rect SDF in logical pixels (UV * rect_size), so margin/radius stay resolution
    // independent. Opaque frame outside the rounded rect, transparent inside, 1px stroke inside.
    // The same shader runs a rail's widened left contour band with a larger left margin.
    private const string FrameShaderCode = """
        shader_type canvas_item;

        uniform vec2 rect_size = vec2(1920.0, 1080.0);
        // Left, top, right, bottom thickness. Symmetric for the frame; a rail's left contour band
        // widens only the left one, so its fill continues this boundary instead of stopping at it.
        uniform vec4 margin = vec4(16.0);
        uniform float radius = 16.0;
        uniform vec4 frame_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);
        uniform vec4 edge_color : source_color = vec4(1.0, 1.0, 1.0, 0.10);
        uniform float edge_width = 1.0;
        // Left x from which the widened band's own edge continues this inner boundary, so this layer's
        // stroke stops there (< 0.0 draws it the whole way round).
        uniform float stroke_cut_x = -1.0;
        // > 0.0: this layer only draws above this y, so the narrow frame edge takes over below.
        uniform float drop_below_y = -1.0;
        // > 0.0: arc length the band bends its widened edge back over, ending at drop_below_y.
        uniform float drop_below_curve = 0.0;
        // > 0.0: the stroke cut above only applies above this y (< 0.0 cuts all the way down).
        uniform float stroke_cut_end_y = -1.0;
        // > 0.0: this layer's own inner stroke is also suppressed along the right edge between these two
        // y's, so a right-edge band of the same fill continues the boundary instead of leaving a seam.
        uniform float stroke_cut_y0 = -1.0;
        uniform float stroke_cut_y1 = -1.0;

        float sd_round_box(vec2 p, vec2 half_size, float r) {
            vec2 q = abs(p) - half_size + r;
            return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
        }

        void fragment() {
            vec2 p = UV * rect_size;
            // Godot forbids 'return' in fragment: fade the whole output instead (0 below the step).
            float present = drop_below_y > 0.0 && p.y >= drop_below_y ? 0.0 : 1.0;
            // Bending the thickness, not cutting it, leaves the SDF's own edge as a tangent arc.
            float left = margin.x;
            if (drop_below_y > 0.0 && drop_below_curve > 0.0)
                left = mix(margin.x, margin.z, smoothstep(drop_below_y - drop_below_curve, drop_below_y, p.y));

            vec2 half_size = max((rect_size - vec2(left + margin.z, margin.y + margin.w)) * 0.5, vec2(0.0));
            vec2 hole_center = vec2(left, margin.y) + half_size;
            float d = sd_round_box(p - hole_center, half_size, min(radius, min(half_size.x, half_size.y)));

            vec3 col = frame_color.rgb;
            float a = frame_color.a * smoothstep(-0.5, 0.5, d);

            // The right cut only ever reaches the boundary at the hole's own right edge, so the left edge
            // is never touched even when a band's span overlaps it.
            bool cut = (p.x < stroke_cut_x && (stroke_cut_end_y <= 0.0 || p.y < stroke_cut_end_y))
                || (stroke_cut_y0 > 0.0 && p.x > rect_size.x - margin.z - 1.0
                    && p.y >= stroke_cut_y0 && p.y < stroke_cut_y1);
            float edge = cut ? 0.0 : 1.0 - smoothstep(0.0, edge_width, abs(d));

            col = mix(col, edge_color.rgb, edge * edge_color.a);
            a = max(a, edge * edge_color.a);

            COLOR = vec4(col, a * present);
        }
        """;
}
