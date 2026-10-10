using Godot;
using KitLib.Abstractions.Modding;

namespace KitLib.UI;

internal sealed class SidebarModRowVm {
    public required KitLibModEntry Entry { get; init; }
    public required Label TitleLabel { get; init; }
    public required StyleBoxFlat InnerStyle { get; init; }
    public required Panel BgPanel { get; init; }
    public required Control Host { get; init; }
    public required Control ActionsBar { get; init; }
    public required Control Content { get; init; }
    public required float ContentBaseLeft { get; init; }
    public required float ContentBaseRight { get; init; }
    public required float SwipeMaxOffset { get; init; }
    public bool Pressing;

    public string Id => Entry.Id;

    public bool IsSelectable => Entry.IsLoaded;
}
