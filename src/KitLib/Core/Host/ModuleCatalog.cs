using System.Collections.Generic;

namespace KitLib.Host;

internal static class ModuleCatalog {
    static readonly HashSet<string> Loaded = new(StringComparer.OrdinalIgnoreCase);
    static readonly List<object> Modules = [];

    internal static void Announce(string moduleId) {
        if (string.IsNullOrWhiteSpace(moduleId)) return;
        Loaded.Add(moduleId);
    }

    internal static void Register(object module) {
        ArgumentNullException.ThrowIfNull(module);
        var id = HostReflection.GetStringProperty(module, "Id");
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Module must expose a non-empty Id property.", nameof(module));

        Announce(id);
        Modules.Add(module);
        try {
            HostReflection.InvokeParameterless(module, "OnInitialize");
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib module {id} init failed: {ex.Message}");
        }
    }

    internal static bool IsLoaded(string moduleId) =>
        !string.IsNullOrWhiteSpace(moduleId) && Loaded.Contains(moduleId);

    internal static IReadOnlyCollection<string> LoadedModuleIds => Loaded;
}
