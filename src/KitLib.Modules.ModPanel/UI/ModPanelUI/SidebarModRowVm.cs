using Godot;

namespace KitLib.UI;

internal sealed class SidebarModRowVm {
    public required string Id { get; init; }
    public required StyleBoxFlat InnerStyle { get; init; }
    public required Control Host { get; init; }
    public bool Pressing;
}
