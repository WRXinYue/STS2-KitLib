using System;
using System.Collections.Generic;
using System.Reflection;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.Modding;

internal static class ModManagerLoadedMods {
    private static readonly Lazy<Func<IEnumerable<Mod>>> _getLoadedMods =
        new(() => {
            MethodInfo? method = typeof(ModManager).GetMethod("GetLoadedMods",
                BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

            if (method == null)
                throw new InvalidOperationException("ModManager.GetLoadedMods() not found. Cannot enumerate loaded mods.");

            return () => (IEnumerable<Mod>)method.Invoke(null, null)!;
        }, isThreadSafe: true);

    internal static IEnumerable<Mod> Enumerate() => _getLoadedMods.Value();
}
