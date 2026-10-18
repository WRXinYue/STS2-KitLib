using System.Collections.Generic;
using Godot;

namespace KitLib.UI;

/// <summary>
/// Backdrop for the combat rings band: a rounded-rectangle body pinned by its right edge to the screen
/// frame, with a right-angled triangle flare spliced onto its top and bottom. Each flare keeps its right
/// angle on that edge, and its hypotenuse fairs into the body edge through one long blend curve, so the
/// whole outline reads as a single smooth contour instead of a rectangle with tabs stuck on.
/// </summary>
internal sealed partial class CombatRingBand : Control {
    /// <summary>Width of the plain rectangle body, in pixels.</summary>
    public const float BodyWidth = 50f;
    /// <summary>Vertical size of the top / bottom flare, in pixels.</summary>
    public const float FlareHeight = 24f;
    /// <summary>Round on the body's two left corners; a larger round also shortens the junction blend.</summary>
    private const float CornerRadius = 16f;

    public Color BgColor = new(0.055f, 0.055f, 0.07f, 0.86f);

    private readonly TextureRect _backdrop;
    private Vector2I _builtSize;

    public CombatRingBand() {
        _backdrop = new TextureRect {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_backdrop);
        Resized += Rebuild;
    }

    /// <summary>Horizontal center of the body on a row — shared with the HUD so rows ride the same contour.</summary>
    public static float CenterlineAt(float y, float height, float width) =>
        width - BandWidthAt(y, height) * 0.5f;

    /// <summary>Left boundary of the silhouette at a row, flares included — lets the HUD tuck chrome into the notch.</summary>
    public static float LeftEdgeAt(float y, float height, float width) {
        int w = Mathf.RoundToInt(width);
        int h = Mathf.RoundToInt(height);
        if (w <= 0 || h <= 0)
            return width;

        var left = LeftBoundary(w, h);
        return left[Mathf.Clamp(Mathf.RoundToInt(y), 0, h - 1)];
    }

    /// <summary>Flare size a band this tall can carry before the body stops reading as a body.</summary>
    private static float FlareFor(float height) => Mathf.Min(FlareHeight, height * 0.25f);

    /// <summary>Corner round the body can still afford beside the flares.</summary>
    private static float CornerFor(float height) =>
        Mathf.Min(CornerRadius, Mathf.Max(height * 0.5f - FlareFor(height) - 1f, 1f));

    /// <summary>Regenerates the silhouette at the current size; scaling a texture would soften the curves.</summary>
    private void Rebuild() {
        int w = Mathf.RoundToInt(Size.X);
        int h = Mathf.RoundToInt(Size.Y);
        if (w <= 0 || h <= 0 || (w == _builtSize.X && h == _builtSize.Y))
            return;

        _builtSize = new Vector2I(w, h);
        _backdrop.Texture = BuildSilhouette(w, h);
    }

    /// <summary>Rasterizes the contour as one alpha row per scanline.</summary>
    private ImageTexture BuildSilhouette(int w, int h) {
        float[] left = LeftBoundary(w, h);
        var image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);

        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                // Partial alpha on the boundary pixel is what anti-aliases the contour.
                float coverage = Mathf.Clamp(x + 1f - left[y], 0f, 1f);
                image.SetPixel(x, y, new Color(BgColor.R, BgColor.G, BgColor.B, BgColor.A * coverage));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>Leftmost material x on every row, taken from the sampled contour so curves stay true.</summary>
    private static float[] LeftBoundary(int w, int h) {
        var left = new float[h];
        for (int i = 0; i < h; i++)
            left[i] = w;

        var pts = Contour(w, h);
        for (int i = 0; i + 1 < pts.Count; i++)
            Carve(left, pts[i], pts[i + 1]);
        return left;
    }

    /// <summary>Records what one contour segment contributes to each row it crosses.</summary>
    private static void Carve(float[] left, Vector2 a, Vector2 b) {
        int from = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.Y, b.Y) - 0.5f));
        int to = Mathf.Min(left.Length - 1, Mathf.CeilToInt(Mathf.Max(a.Y, b.Y) - 0.5f));

        for (int y = from; y <= to; y++) {
            float x = Mathf.IsEqualApprox(a.Y, b.Y)
                ? Mathf.Min(a.X, b.X)
                : Mathf.Lerp(a.X, b.X, Mathf.Clamp((y + 0.5f - a.Y) / (b.Y - a.Y), 0f, 1f));
            if (x < left[y])
                left[y] = x;
        }
    }

    /// <summary>The left contour, top flare tip down to the bottom one, sampled densely.</summary>
    private static List<Vector2> Contour(float w, float h) {
        float flare = FlareFor(h);
        float corner = CornerFor(h);
        float bodyLeft = w - BodyWidth;
        float cornerX = bodyLeft + corner;
        float topBody = flare;
        float bottomBody = h - flare;
        float tipX = w - flare * 0.0875f;
        float tipY = flare * 0.234f;
        float tipRound = flare * 0.25f;

        var pts = new List<Vector2> { new(w, tipRound) };
        // Top tip round, then the blend curve that fairs the hypotenuse into the body's top edge.
        Quadratic(pts, new Vector2(w, tipRound), new Vector2(w, 0f), new Vector2(tipX, tipY));
        Cubic(pts,
            new Vector2(tipX, tipY),
            new Vector2(w - flare * 0.10f, flare * 0.30f),
            new Vector2(w - flare * 0.333f, topBody),
            new Vector2(cornerX, topBody));
        // Body: both left corners rounded, straight left edge between them.
        Arc(pts, new Vector2(cornerX, topBody + corner), corner, -90f, -180f);
        pts.Add(new Vector2(bodyLeft, bottomBody - corner));
        Arc(pts, new Vector2(cornerX, bottomBody - corner), corner, 180f, 90f);
        // Bottom blend curve, then the bottom tip round.
        Cubic(pts,
            new Vector2(cornerX, bottomBody),
            new Vector2(w - flare * 0.333f, bottomBody),
            new Vector2(w - flare * 0.10f, h - flare * 0.30f),
            new Vector2(tipX, h - tipY));
        Quadratic(pts, new Vector2(tipX, h - tipY), new Vector2(w, h), new Vector2(w, h - tipRound));
        return pts;
    }

    /// <summary>Samples a quadratic Bézier onto the contour, skipping the start point.</summary>
    private static void Quadratic(List<Vector2> pts, Vector2 from, Vector2 control, Vector2 to, int steps = 12) {
        for (int i = 1; i <= steps; i++) {
            float t = i / (float)steps;
            pts.Add(from.Lerp(control, t).Lerp(control.Lerp(to, t), t));
        }
    }

    /// <summary>Samples a cubic Bézier onto the contour, skipping the start point.</summary>
    private static void Cubic(List<Vector2> pts, Vector2 from, Vector2 c1, Vector2 c2, Vector2 to, int steps = 24) {
        for (int i = 1; i <= steps; i++) {
            float t = i / (float)steps;
            Vector2 a = from.Lerp(c1, t);
            Vector2 b = c1.Lerp(c2, t);
            Vector2 c = c2.Lerp(to, t);
            pts.Add(a.Lerp(b, t).Lerp(b.Lerp(c, t), t));
        }
    }

    /// <summary>Samples a circular arc onto the contour, skipping the start point.</summary>
    private static void Arc(List<Vector2> pts, Vector2 center, float radius, float fromDeg, float toDeg, int steps = 16) {
        for (int i = 1; i <= steps; i++) {
            float a = Mathf.DegToRad(Mathf.Lerp(fromDeg, toDeg, i / (float)steps));
            pts.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
        }
    }

    /// <summary>Width the body spans on a row, so the HUD can center rows on the same contour.</summary>
    private static float BandWidthAt(float y, float height) {
        float corner = CornerFor(height);
        float top = FlareFor(height) + corner;
        float bottom = height - FlareFor(height) - corner;
        float center = top > bottom ? height * 0.5f : Mathf.Clamp(y, top, bottom);
        float d = Mathf.Abs(y - center);
        return d >= corner ? BodyWidth : BodyWidth - corner + Mathf.Sqrt(corner * corner - d * d);
    }
}
