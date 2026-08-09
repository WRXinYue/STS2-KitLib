using Godot;
using KitLib.Abstractions.Modding;

namespace KitLib.Modding;

/// <summary>
/// Public KitLib-native settings form builders for content-mod <c>BuildBody</c> pages.
/// Requires KitModPanel at runtime (<see cref="IsAvailable"/>).
/// </summary>
public static class ModSettingsUi {
    public static bool IsAvailable => KitLibModSettingsUiApi.IsAvailable;

    public static VBoxContainer CreatePageStack() =>
        (VBoxContainer)Require(KitLibModSettingsUiApi.CreatePageStack)();

    public static Control CreateSectionHeader(string title, string? description = null) =>
        (Control)Require(KitLibModSettingsUiApi.CreateSectionHeader)(title, description);

    public static Control CreateBoolToggle(string title, string? description, Func<bool> get, Action<bool> set) =>
        (Control)Require(KitLibModSettingsUiApi.CreateBoolToggle)(title, description, get, set);

    public static Control CreateChoiceRow(
        string title,
        string? description,
        IReadOnlyList<KitLibModSettingsChoice> options,
        Func<int> getId,
        Action<int> setId) =>
        (Control)Require(KitLibModSettingsUiApi.CreateChoiceRow)(title, description, options, getId, setId);

    public static Control CreateIntSlider(
        string title,
        string? description,
        Func<int> get,
        Action<int> set,
        int min,
        int max,
        int step = 1) =>
        (Control)Require(KitLibModSettingsUiApi.CreateIntSlider)(title, description, get, set, min, max, step);

    public static Control CreateFloatSlider(
        string title,
        string? description,
        Func<float> get,
        Action<float> set,
        float min,
        float max,
        float step = 0.01f) =>
        (Control)Require(KitLibModSettingsUiApi.CreateFloatSlider)(title, description, get, set, min, max, step);

    public static Control CreateStringField(
        string title,
        string? description,
        Func<string> get,
        Action<string> set,
        bool multiline = false) =>
        (Control)Require(KitLibModSettingsUiApi.CreateStringField)(title, description, get, set, multiline);

    public static Control CreateColorRow(
        string title,
        string? description,
        Func<KitLibModSettingsRgb> get,
        Action<KitLibModSettingsRgb> set) =>
        (Control)Require(KitLibModSettingsUiApi.CreateColorRow)(title, description, get, set);

    public static Control CreateActionButton(string title, string? description, Action onPressed) =>
        (Control)Require(KitLibModSettingsUiApi.CreateActionButton)(title, description, onPressed);

    public static void RefreshBoolToggles() => KitLibModSettingsUiApi.RefreshBoolToggles?.Invoke();

    static T Require<T>(T? fn) where T : class {
        if (fn == null)
            throw new InvalidOperationException(
                "KitLib mod settings UI builders are unavailable. Install KitModPanel and ensure it loaded.");
        return fn;
    }
}
