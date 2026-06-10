using System.IO;
using KitLib;

namespace KitLib.Dev;

/// <summary>mod_data root adopted from Core when Dev satellite delegates are wired.</summary>
internal static class DevModDataPaths {
    private static string? _root;

    internal static bool IsSet => !string.IsNullOrEmpty(_root);

    internal static void SetRoot(string path) {
        _root = path;
    }

    internal static string Root {
        get {
            if (string.IsNullOrEmpty(_root))
                throw new InvalidOperationException("Dev mod_data root not adopted from Core.");
            return _root;
        }
    }

    internal static string ScriptsDir => Path.Combine(Root, "scripts");
    internal static string InstancesDir => Path.Combine(Root, "instances");
}
