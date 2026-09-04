namespace KitLib.Abstractions.Modding;

/// <summary>One option for <see cref="KitLibModSettingsUiApi.CreateChoiceRow"/>.</summary>
public readonly record struct KitLibModSettingsChoice(string Label, int Id);

/// <summary>RGB color (0–1) for <see cref="KitLibModSettingsUiApi.CreateColorRow"/> — no Godot types on Abstractions.</summary>
public readonly record struct KitLibModSettingsRgb(float R, float G, float B);

/// <summary>
/// KitLib-native mod-settings form builders, wired by KitModPanel.
/// Content mods should call <c>KitLib.Modding.ModSettingsUi</c> (Core) instead of these delegates.
/// </summary>
public static class KitLibModSettingsUiApi {
    public static bool IsAvailable => CreateBoolToggle != null;

    public static Func<object>? CreatePageStack { get; set; }

    public static Func<string, string?, object>? CreateSectionHeader { get; set; }

    public static Func<string, string?, Func<bool>, Action<bool>, object>? CreateBoolToggle { get; set; }

    public static Func<string, string?, IReadOnlyList<KitLibModSettingsChoice>, Func<int>, Action<int>, object>?
        CreateChoiceRow { get; set; }

    public static Func<string, string?, Func<int>, Action<int>, int, int, int, object>? CreateIntSlider {
        get;
        set;
    }

    public static Func<string, string?, Func<float>, Action<float>, float, float, float, object>? CreateFloatSlider {
        get;
        set;
    }

    public static Func<string, string?, Func<string>, Action<string>, bool, object>? CreateStringField { get; set; }

    public static Func<string, string?, Func<KitLibModSettingsRgb>, Action<KitLibModSettingsRgb>, object>?
        CreateColorRow { get; set; }

    public static Func<string, string?, Action, object>? CreateActionButton { get; set; }

    /// <summary>Refresh live bool toggles after external state changes (optional).</summary>
    public static Action? RefreshBoolToggles { get; set; }
}
