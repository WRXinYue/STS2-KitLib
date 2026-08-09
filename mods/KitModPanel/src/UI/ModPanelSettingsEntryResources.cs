using Godot;
using MegaCrit.Sts2.Core.Assets;

namespace KitLib.UI;

/// <summary>Vanilla settings-screen assets for the KitLib mod panel entry button.</summary>
internal static class ModPanelSettingsEntryResources {
    const string SettingsLineThemePath = "res://themes/settings_screen_line_header.tres";
    const string ButtonFontPath = "res://themes/kreon_bold_glyph_space_two.tres";
    const string ButtonTexturePath = "res://images/ui/reward_screen/reward_skip_button.png";
    const string HsvShaderPath = "res://shaders/hsv.gdshader";

    internal static Theme SettingsLineTheme =>
        PreloadManager.Cache.GetAsset<Theme>(SettingsLineThemePath);

    internal static Font ButtonFont =>
        PreloadManager.Cache.GetAsset<Font>(ButtonFontPath);

    internal static Texture2D ButtonTexture =>
        PreloadManager.Cache.GetAsset<Texture2D>(ButtonTexturePath);

    internal static ShaderMaterial CreateAccentButtonMaterial() =>
        CreateHsvShaderMaterial(0.82f, 1.4f, 0.8f);

    internal static Color AccentButtonOutlineColor => new(0.1274f, 0.26f, 0.14066f);

    static ShaderMaterial CreateHsvShaderMaterial(float h, float s, float v) {
        var shader = (Shader?)GD.Load<Shader>(HsvShaderPath)?.Duplicate()
            ?? throw new System.InvalidOperationException($"Failed to load HSV shader ({HsvShaderPath}).");
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("h", h);
        material.SetShaderParameter("s", s);
        material.SetShaderParameter("v", v);
        return material;
    }
}
