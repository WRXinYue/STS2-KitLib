using Godot;
using KitLib.Icons;

namespace KitLib.UI;

/// <summary>Arc gauge for one player: track ring, radially filled value arc and a centered metric glyph.</summary>
internal sealed partial class CombatStatRing : Control {
    private const int RingSizePx = 28;
    private const int RingGlyphSize = 12;
    private const float StrokeWidthPx = 2.5f;

    private static readonly Color TrackColor = new(1f, 1f, 1f, 0.30f);

    private static Texture2D? _ringTexture;

    private readonly TextureProgressBar _arc;
    private readonly TextureRect _glyph;

    public float Fraction;
    public Color ValueColor = Colors.White;
    public ImageTexture? Glyph;

    public CombatStatRing() {
        CustomMinimumSize = new Vector2(RingSizePx, RingSizePx);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Stop;

        _arc = new TextureProgressBar {
            Name = "Arc",
            TextureUnder = RingTexture,
            TextureProgress = RingTexture,
            FillMode = (int)TextureProgressBar.FillModeEnum.Clockwise,
            RadialInitialAngle = -90f,
            RadialFillDegrees = 360f,
            MinValue = 0,
            MaxValue = 1,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _arc.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_arc);

        _glyph = new TextureRect {
            Name = "Glyph",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _glyph.AnchorLeft = 0.5f;
        _glyph.AnchorTop = 0.5f;
        _glyph.AnchorRight = 0.5f;
        _glyph.AnchorBottom = 0.5f;
        _glyph.OffsetLeft = -RingGlyphSize / 2f;
        _glyph.OffsetTop = -RingGlyphSize / 2f;
        _glyph.OffsetRight = RingGlyphSize / 2f;
        _glyph.OffsetBottom = RingGlyphSize / 2f;
        AddChild(_glyph);
    }

    /// <summary>Pushes the current values into the built-in nodes (no custom drawing involved).</summary>
    public void Apply() {
        _arc.TintUnder = TrackColor;
        _arc.TintProgress = ValueColor;
        _arc.Value = Mathf.Clamp(Fraction, 0f, 1f);
        _glyph.Texture = Glyph;
    }

    /// <summary>Procedural annulus, generated once at runtime (no asset shipped).</summary>
    private static Texture2D RingTexture {
        get {
            if (_ringTexture != null)
                return _ringTexture;

            var image = Image.CreateEmpty(RingSizePx, RingSizePx, false, Image.Format.Rgba8);
            float center = RingSizePx / 2f;
            float outer = center - 0.5f;
            float inner = outer - StrokeWidthPx;

            for (int y = 0; y < RingSizePx; y++) {
                for (int x = 0; x < RingSizePx; x++) {
                    float dx = x + 0.5f - center;
                    float dy = y + 0.5f - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float outside = Mathf.Clamp(outer - distance + 0.5f, 0f, 1f);
                    float inside = Mathf.Clamp(distance - inner + 0.5f, 0f, 1f);
                    image.SetPixel(x, y, new Color(1f, 1f, 1f, outside * inside));
                }
            }

            _ringTexture = ImageTexture.CreateFromImage(image);
            return _ringTexture;
        }
    }
}
