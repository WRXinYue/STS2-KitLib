using KitLib.AI.Knowledge;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.AI.Sts2.Patches;

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.Init))]
internal static class ModelDbInitPatch {
    [HarmonyPostfix]
    static void Postfix() => AiKnowledgeBootstrap.EnsureRegistered();
}
