using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;

namespace KitLib.Singleplayer.Companion.Patches;

[HarmonyPatch(typeof(NMultiplayerPlayerState), nameof(NMultiplayerPlayerState._Ready))]
internal static class SpvCompanionPlayerStateNamePatch {
    static readonly System.Reflection.FieldInfo NameplateField =
        AccessTools.Field(typeof(NMultiplayerPlayerState), "_nameplateLabel")!;

    [HarmonyPostfix]
    static void Postfix(NMultiplayerPlayerState __instance) {
        if (!SpvCompanionDisplayNames.ShouldOverride(__instance.Player))
            return;

        if (NameplateField.GetValue(__instance) is not MegaLabel label)
            return;

        label.SetTextAutoSize(SpvCompanionDisplayNames.Resolve(__instance.Player));
    }
}
