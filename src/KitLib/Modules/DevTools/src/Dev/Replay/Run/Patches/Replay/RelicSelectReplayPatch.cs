using Godot;
using HarmonyLib;
using KitLib.Replay.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;

namespace KitLib.Replay.Patches.Replay;

/// <summary>
/// Captures <see cref="NChooseARelicSelection"/> (trait-point picks, Neow relics, etc.),
/// records the clicked option, and wakes the dispatcher for replay.
/// </summary>
[HarmonyPatch(typeof(NChooseARelicSelection), "_Ready")]
public static class RelicSelectReplayPatch {
    private const string WiredMeta = "KitLibRelicReplayWired";
    private const string ScreenMeta = "KitLibRelicReplayScreen";

    [HarmonyPostfix]
    public static void Postfix(NChooseARelicSelection __instance) {
        ReplayState.ActiveRelicScreen = __instance;
        if (!__instance.HasMeta(ScreenMeta)) {
            __instance.SetMeta(ScreenMeta, true);
            __instance.TreeExiting += () => {
                if (ReplayState.ActiveRelicScreen == __instance)
                    ReplayState.ActiveRelicScreen = null;
            };
        }

        WireRecording(__instance);
        Callable.From(() => WireRecording(__instance)).CallDeferred();
        if (ReplayEngine.IsActive)
            ReplayDispatcher.DispatchNow();
    }

    static void WireRecording(NChooseARelicSelection screen) {
        if (!GodotObject.IsInstanceValid(screen) || !screen.IsInsideTree())
            return;

        var choices = ChooseRelicCommand.ListChoices(screen);
        for (int i = 0; i < choices.Count; i++) {
            if (choices[i] is not NClickableControl click)
                continue;
            if (click.HasMeta(WiredMeta))
                continue;
            click.SetMeta(WiredMeta, true);

            int index = i;
            var relic = choices[i] is NRelicCollectionEntry entry ? entry.relic : null;
            click.Connect(NClickableControl.SignalName.Released, Callable.From(() => {
                if (ReplayEngine.IsActive)
                    return;
                var cmd = new ChooseRelicCommand(index) {
                    Comment = RelicComment(relic),
                };
                PlayerActionBuffer.Record(cmd.ToLogString());
            }));
        }
    }

    static string? RelicComment(RelicModel? relic) {
        if (relic == null)
            return null;
        string id = ((AbstractModel)relic).Id.Entry;
        string title = relic.Title.GetFormattedText();
        return string.IsNullOrEmpty(title) ? id : $"{id} {title}";
    }
}
