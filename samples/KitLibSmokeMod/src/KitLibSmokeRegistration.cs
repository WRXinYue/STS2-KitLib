extern alias KitLibCore;

using KitLib.Abstractions.Modding;
using KitLibCore::KitLib.Companion;

namespace KitLibSmokeMod;

/// <summary>KitLib content-mod integration used by <see cref="MainFile"/> and CI load tests.</summary>
internal static class KitLibSmokeRegistration {
    public static void Register() {
        KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
            ModId = "KitLibSmokeMod",
            PageId = "smoke",
            Title = "Smoke",
            SortOrder = 0,
            BuildBody = static () => new object(),
        });
    }
}
