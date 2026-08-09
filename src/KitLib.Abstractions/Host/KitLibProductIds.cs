using System;
using System.Collections.Generic;

namespace KitLib.Abstractions.Host;

/// <summary>Workshop / game-folder product ids and which satellite modules each product owns.</summary>
public static class KitLibProductIds {
    public const string KitLib = "KitLib";
    public const string KitModPanel = "KitModPanel";
    public const string KitDevTools = "KitDevTools";
    public const string KitAI = "KitAI";

    public static readonly string[] All = [KitLib, KitModPanel, KitDevTools, KitAI];

    /// <summary>Satellite module ids shipped under each product (excludes Core/Loader).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> ModulesByProduct =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) {
            [KitLib] = [],
            [KitModPanel] = [KitLibModuleIds.ModPanel],
            [KitDevTools] = [KitLibModuleIds.Panel, KitLibModuleIds.Dev],
            [KitAI] = [KitLibModuleIds.Ai],
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
