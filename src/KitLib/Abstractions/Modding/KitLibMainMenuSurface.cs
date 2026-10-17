namespace KitLib.Abstractions.Modding;

/// <summary>
/// Main-menu screen an entry registered through <see cref="KitLibMainMenuCornerButtonRegistry"/>
/// belongs to. An entry renders only while its own surface is on screen.
/// </summary>
public enum KitLibMainMenuSurface {
    /// <summary>Title screen, while no submenu is open.</summary>
    MainMenu = 0,

    /// <summary>Vanilla character select, while it is the open submenu.</summary>
    CharacterSelect = 1,
}
