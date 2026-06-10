using Godot;
using KitLib.Abstractions.Modding;
using KitLib.Integration;

namespace KitLib.UI;

internal sealed class SidebarModRowVm {
    public required KitLibModEntry Entry { get; init; }
    public required StyleBoxFlat InnerStyle { get; init; }
    public required Panel BgPanel { get; init; }
    public required Control Host { get; init; }
    public ModPanelEnableTickbox? EnableTickbox { get; init; }
    public bool Pressing;

    public string Id => Entry.Id;

    public bool IsSelectable => Entry.IsLoaded;
}
