using System.Linq;
using KitLib.Abstractions.Modding;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib.Modding;

/// <summary><see cref="IModLoadSettings"/> backed by <c>SaveManager.SettingsSave.ModSettings</c>.</summary>
public sealed class Sts2ModLoadSettings : IModLoadSettings {
    public static IModLoadSettings Default { get; } = new Sts2ModLoadSettings();

    private Sts2ModLoadSettings() { }

    public bool IsEnabled(string id, ModEntrySource source) {
        var settings = TryGetModSettings();
        if (settings == null)
            return true;
        return !settings.IsModDisabled(id, Sts2ModRegistry.ToStsSource(source));
    }

    public void SetEnabled(string id, ModEntrySource source, bool enabled) {
        var settingsSave = SaveManager.Instance.SettingsSave;
        if (settingsSave.ModSettings == null)
            settingsSave.ModSettings = new ModSettings();
        var settings = settingsSave.ModSettings;
        var stsSource = Sts2ModRegistry.ToStsSource(source);
        SettingsSaveMod? row = null;
        foreach (var mod in settings.ModList) {
            if (mod.Id == id && mod.Source == stsSource) {
                row = mod;
                break;
            }
        }
        if (row == null) {
            row = new SettingsSaveMod { Id = id, Source = stsSource, IsEnabled = enabled };
            settings.ModList.Add(row);
        }
        else {
            row.IsEnabled = enabled;
        }
    }

    public bool HasPendingRestartChanges() {
        var pairs = Sts2ModRegistry.Default.GetAllEntries()
            .Select(e => (e.LoadStatus, e.IsEnabledInSettings));
        return ModLoadSettingsPendingChanges.AnyPendingRestart(pairs);
    }

    public void Persist() => SaveManager.Instance.SaveSettings();

    internal static ModSettings? TryGetModSettings()
        => SaveManager.Instance?.SettingsSave?.ModSettings;
}
