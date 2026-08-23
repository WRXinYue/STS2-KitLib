using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace KitLib.Replay.Commands;

/// <summary>
/// Choose an event option by index.
/// Recorded as: "ChooseEventOption {index} # {textKey}"
///
/// Index -1 represents PROCEED (event finished or about to transition).
/// The text key is stored as a comment for human readability only.
/// </summary>
public class ChooseEventOptionCommand : ReplayCommand {
    private const string Prefix = "ChooseEventOption ";
    private const int ProceedIndex = -1;

    public int RecordedIndex { get; }

    public ChooseEventOptionCommand(int recordedIndex) : base("") {
        RecordedIndex = recordedIndex;
    }

    public override string ToString() => $"{Prefix}{RecordedIndex}";

    public override string Describe()
        => RecordedIndex == ProceedIndex
            ? "proceed from event"
            : $"choose event option index={RecordedIndex}" +
              (Comment != null ? $" ({Comment})" : "");

    public override ExecuteResult Execute() {
        var sync = ReplayState.ActiveEventSynchronizer;

        // PROCEED sentinel — always consume, even if the sync has been cleared
        // or we're no longer in an EventRoom.  Some events auto-proceed when
        // their sub-combat ends (Battleworn Dummy), so the recorded `-1` has
        // no UI to act on at replay time.
        if (RecordedIndex == ProceedIndex) {
            bool finished = sync != null && sync.Events.Count > 0 && sync.Events[0].IsFinished;
            if (!finished)
                return ExecuteResult.Ok();
            if (ReplayLiveMode.ConsumeThink(this, out int proceedThinkMs))
                return ExecuteResult.Retry(proceedThinkMs);
            TaskHelper.RunSafely(NEventRoom.Proceed());
            ScheduleFollowUp();
            return ExecuteResult.Ok();
        }

        if (sync == null)
            return ExecuteResult.Retry(300);

        // Event finished — consume PROCEED and advance.
        if (sync.Events.Count > 0 && sync.Events[0].IsFinished) {
            TaskHelper.RunSafely(NEventRoom.Proceed());
            ScheduleFollowUp();
            return ExecuteResult.Ok();
        }

        if (sync.Events.Count == 0)
            return ExecuteResult.Retry(300);

        var options = sync.Events[0].CurrentOptions;
        if (RecordedIndex < 0 || RecordedIndex >= options.Count)
            return ExecuteResult.Retry(300);

        var buttons = FindVisibleOptionButtons();
        bool mapOpen = NMapScreen.Instance is { IsOpen: true };
        if (buttons.Count == 0 || mapOpen)
            return ExecuteResult.Retry(200);

        if (RecordedIndex >= buttons.Count)
            return ExecuteResult.Retry(200);

        if (ReplayLiveMode.ConsumeThink(this, out int thinkMs))
            return ExecuteResult.Retry(thinkMs);

        buttons[RecordedIndex].ForceClick();
        ScheduleFollowUp();
        return ExecuteResult.Ok();
    }

    static List<NEventOptionButton> FindVisibleOptionButtons() {
        var found = new List<NEventOptionButton>();
        var root = NGame.Instance?.GetTree()?.Root;
        if (root == null)
            return found;

        Node? eventRoom = root.GetNodeOrNull("/root/Game/RootSceneContainer/Run/RoomContainer/EventRoom");
        Node search = eventRoom is CanvasItem roomCi && roomCi.IsVisibleInTree() ? eventRoom : root;
        foreach (Node node in search.FindChildren("*", "", owned: false)) {
            if (node is not NEventOptionButton btn)
                continue;
            if (!btn.Visible || !btn.IsVisibleInTree())
                continue;
            if (btn.Option != null && btn.Option.IsLocked)
                continue;
            found.Add(btn);
        }

        found.Sort((a, b) => {
            int y = a.GlobalPosition.Y.CompareTo(b.GlobalPosition.Y);
            return y != 0 ? y : a.GlobalPosition.X.CompareTo(b.GlobalPosition.X);
        });
        return found;
    }

    private static void ScheduleFollowUp() {
        float delay = ReplayLiveMode.Enabled ? 0.2f : 0f;
        if (delay <= 0) {
            ReplayDispatcher.DispatchNow();
            return;
        }
        NGame.Instance!.GetTree()!.CreateTimer(delay).Connect(
            "timeout", Callable.From(() => {
                ReplayDispatcher.DispatchNow();
            }));
    }

    public static ChooseEventOptionCommand? TryParse(string raw) {
        if (!raw.StartsWith(Prefix))
            return null;

        string rest = raw.Substring(Prefix.Length).Trim();

        if (int.TryParse(rest, out int idx))
            return new ChooseEventOptionCommand(idx);

        return null;
    }
}
