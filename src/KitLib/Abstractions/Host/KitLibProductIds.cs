using System;
using System.Collections.Generic;

namespace KitLib.Abstractions.Host;

/// <summary>
/// Workshop / game-folder product ids and which satellite modules each product owns.
/// KitLib is the single published product; the mod panel, in-game panel and dev tools
/// all ship as satellite DLLs under <c>mods/KitLib/lib/&lt;api&gt;/modules/</c>.
/// </summary>
public static class KitLibProductIds {
    public const string KitLib = "KitLib";

    public static readonly string[] All = [KitLib];

    /// <summary>Satellite module ids shipped under each product (excludes Core/Loader).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> ModulesByProduct =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) {
            [KitLib] = [KitLibModuleIds.ModPanel, KitLibModuleIds.Panel, KitLibModuleIds.Dev],
        };

    public static string? TryGetProductIdForModule(string moduleId) {
        foreach (var (productId, modules) in ModulesByProduct) {
            foreach (var id in modules) {
                if (string.Equals(id, moduleId, StringComparison.OrdinalIgnoreCase))
                    return productId;
            }
        }

        return null;
    }

    public static bool ProductOwnsModule(string productId, string moduleId) {
        if (!ModulesByProduct.TryGetValue(productId, out var modules))
            return false;
        foreach (var id in modules) {
            if (string.Equals(id, moduleId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
