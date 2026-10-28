using Godot;

namespace KitLib.Icons;

/// <summary>
/// Typed accessor for Material Design Icons (Iconify kebab names).
///
/// <para><b>Static fields</b>: most live in <c>MdiIcon.Generated.cs</c> (Pascal → kebab). Run
/// <c>scripts/shake_icons.py</c> (or build) to refresh after adding <c>MdiIcon.Xxx</c> usages.</para>
///
/// <para><b>Adding an icon</b></para>
/// <list type="bullet">
/// <item><description><c>MdiIcon.From("book-open-variant")</c> — use the exact Iconify / MDI id string (same as in other ecosystems).</description></item>
/// <item><description><c>MdiIcon.Play</c> etc. when the id is default Pascal→kebab (generated in <c>MdiIcon.Generated.cs</c>).</description></item>
/// <item><description><c>MdiIcon.Get("kebab")</c> — returns <see cref="ImageTexture"/> only; no <c>MdiIcon</c> value.</description></item>
/// </list>
///
/// <para>Missing names in <c>icons/mdi/icons.json</c> fail the shake step. <c>mdi-used.json</c> stays tree-shaken for the DLL.</para>
/// </summary>
public readonly partial struct MdiIcon {
    public string Name { get; }

    private MdiIcon(string kebabName) => Name = kebabName;

    /// <summary>Build from the exact Material Design / Iconify kebab id (e.g. <c>book-open-variant</c>).</summary>
    public static MdiIcon From(string kebabName) => new(kebabName);

    /// <summary>Get the icon as a Godot <see cref="ImageTexture"/>.</summary>
    /// <param name="size">Pixel size (square). Default 24.</param>
    /// <param name="color">Tint colour. Default white.</param>
    public ImageTexture? Texture(int size = 24, Color? color = null)
        => IconifyAdapter.Get(Name, size, color);

    /// <summary>Shorthand: get icon at specific size with default colour.</summary>
    public ImageTexture? this[int size]
        => IconifyAdapter.Get(Name, size);

    /// <summary>Check if this icon is available in the bundled set.</summary>
    public bool IsAvailable => IconifyAdapter.Has(Name);

    /// <summary>Get any icon by kebab-case name.</summary>
    public static ImageTexture? Get(string kebabName, int size = 24, Color? color = null)
        => IconifyAdapter.Get(kebabName, size, color);
}
