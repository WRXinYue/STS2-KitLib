using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;

namespace KitLib.ModVariantLoader;

[HarmonyPatch(typeof(ReflectionHelper), nameof(ReflectionHelper.ModTypes), MethodType.Getter)]
internal static class ReflectionHelperModTypesPatch {
    private static void Postfix(ref Type[] __result) {
        var variantTypes = ModVariantRegistry.GetVariantModTypes();
        if (variantTypes.Length == 0)
            return;

        __result = __result.Concat(variantTypes).Distinct().ToArray();
    }
}
