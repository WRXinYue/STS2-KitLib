using System.Collections.Generic;
using KitLib.Abstractions.Host;

namespace KitLib.Host;

internal static class ModuleCatalog {
    static readonly HashSet<string> Loaded = new(StringComparer.OrdinalIgnoreCase);
    static readonly List<IKitLibModule> Modules = [];

    internal static void Announce(string moduleId) {
        if (string.IsNullOrWhiteSpace(moduleId)) return;
        Loaded.Add(moduleId);
    }

    internal static void Register(IKitLibModule module) {
        if (module == null) throw new ArgumentNullException(nameof(module));
        Announce(module.Id);
        Modules.Add(module);
        try {
            module.OnInitialize();
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib module {module.Id} init failed: {ex.Message}");
        }
    }

    internal static bool IsLoaded(string moduleId) =>
        !string.IsNullOrWhiteSpace(moduleId) && Loaded.Contains(moduleId);

    internal static IReadOnlyCollection<string> LoadedModuleIds => Loaded;
}
