using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;

namespace KitLib.Replay.Commands;

/// <summary>
/// Pick an offered relic on <see cref="NChooseARelicSelection"/> by visual index.
/// Covers LustTravel2 trait-point picks (fake starter relics) and any other relic picker.
/// Recorded as: "ChooseRelic {index} # {relicId}"
/// </summary>
public sealed class ChooseRelicCommand : ReplayCommand {
    private const string Prefix = "ChooseRelic ";

    public int Index { get; }

    public ChooseRelicCommand(int index) : base("") {
        Index = index;
    }

    public override string ToString() => $"{Prefix}{Index}";

    public override string Describe()
        => Comment != null
            ? $"choose relic [{Index}] ({Comment})"
            : $"choose relic [{Index}]";

    public override ExecuteResult Execute() {
        var screen = ReplayState.ActiveRelicScreen;
        if (screen == null || !GodotObject.IsInstanceValid(screen) || !screen.IsInsideTree())
            return ExecuteResult.Retry(200);

        var clickable = FindClickable(screen, Index);
        if (clickable == null)
            return ExecuteResult.Retry(200);

        clickable.ForceClick();
        return ExecuteResult.Ok();
    }

    public static ChooseRelicCommand? TryParse(string raw) {
        if (!raw.StartsWith(Prefix))
            return null;
        return int.TryParse(raw.AsSpan(Prefix.Length).Trim(), out int index)
            ? new ChooseRelicCommand(index)
            : null;
    }

    internal static List<Node> ListChoices(NChooseARelicSelection screen) {
        var entries = new List<Control>();
        foreach (Node node in screen.FindChildren("*", "", owned: false)) {
            if (node is NRelicCollectionEntry { Visible: true } entry)
                entries.Add(entry);
        }

        if (entries.Count == 0) {
            foreach (Node node in screen.FindChildren("*", "", owned: false)) {
                if (node is NTreasureRoomRelicHolder { Visible: true, IsEnabled: true } holder)
                    entries.Add(holder);
            }
        }

        entries.Sort((a, b) => a.GlobalPosition.X.CompareTo(b.GlobalPosition.X));
        var choices = new List<Node>(entries.Count);
        foreach (var entry in entries)
            choices.Add(entry);
        return choices;
    }

    static NClickableControl? FindClickable(NChooseARelicSelection screen, int index) {
        var choices = ListChoices(screen);
        if (index < 0 || index >= choices.Count)
            return null;
        return choices[index] as NClickableControl;
    }
}
