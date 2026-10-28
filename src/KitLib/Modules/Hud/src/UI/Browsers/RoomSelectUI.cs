using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>Room teleport panel — lets the developer jump directly into any room type.</summary>
internal static partial class RoomSelectUI {
    private const string RootName = "KitLibRoomSelect";
    private const string DualMetaKey = "dm_dual_room_select";
    private const string CarrierNodeName = "RoomSelectDualCarrier";
    private const float PanelW = 420f;
    private const float DefaultExtWidth = 420f;

    public static void Show(NGlobalUi globalUi) {
        Remove(globalUi);

        var dual = DevPanelUI.CreateDualColumnOverlay(new DevPanelUI.DualColumnOverlayOptions {
            GlobalUi = globalUi,
            RootName = RootName,
            DualMetaKey = DualMetaKey,
            CarrierNodeName = CarrierNodeName,
            MainDefaultWidth = PanelW,
            ExtDefaultWidth = DefaultExtWidth,
            FallbackClose = () => Remove(globalUi),
        });

        var main = BuildMainPanel(dual.MainContent);
        var ancients = BuildAncientsPanel(dual, main.WarnLabel, globalUi);

        ancients.BackButton.Pressed += ancients.OnBackPressed;
        main.ListHost.AddChild(BuildAncientEntryCard(() => {
            ancients.ResetToList();
            dual.ToggleExtension();
        }));

        dual.AttachToScene();
    }

    public static void Remove(NGlobalUi globalUi)
        => ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();

    internal static void RequestClose(NGlobalUi globalUi) =>
        DevPanelUI.RequestCloseBrowserOverlay(globalUi, RootName, () => Remove(globalUi));
}
