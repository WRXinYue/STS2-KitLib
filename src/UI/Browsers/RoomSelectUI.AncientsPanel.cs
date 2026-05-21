using System.Linq;
using DevMode.Actions;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace DevMode.UI;

internal static partial class RoomSelectUI {
    internal readonly record struct AncientsPanelHandle(
        Button BackButton,
        Label StatusLabel);

    private static AncientsPanelHandle BuildAncientsPanel(VBoxContainer extVbox, Label warnLabel) {
        var backBtn = BuildAncientExtensionHeader(extVbox);

        var ancientScroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var ancientList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        ancientList.AddThemeConstantOverride("separation", 3);
        ancientScroll.AddChild(ancientList);
        extVbox.AddChild(ancientScroll);

        var extStatusLabel = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center };
        extStatusLabel.AddThemeFontSizeOverride("font_size", 11);
        extStatusLabel.AddThemeColorOverride("font_color", DevModeTheme.Subtle);
        extVbox.AddChild(extStatusLabel);

        foreach (var ancient in EventActions.GetAllAncients().OrderBy(EventActions.GetEventDisplayName)) {
            var captured = ancient;
            var btn = DevPanelUI.CreateListItemButton(FormatAncientListLabel(captured));
            btn.Alignment = HorizontalAlignment.Left;
            var epithet = GetAncientEpithet(captured);
            if (!string.IsNullOrWhiteSpace(epithet))
                btn.TooltipText = epithet;
            btn.Pressed += () => {
                if (!RoomActions.IsRunInProgress) {
                    warnLabel.Visible = true;
                    extStatusLabel.Text = "";
                    return;
                }

                warnLabel.Visible = false;
                bool ok = EventActions.TryForceEnterEvent(captured);
                extStatusLabel.Text = ok
                    ? I18N.T("room.entered", "Entering: {0}", EventActions.GetEventDisplayName(captured))
                    : I18N.T("room.error", "Failed to enter room.");
            };
            ancientList.AddChild(btn);
        }

        extStatusLabel.Text = I18N.T("room.ancients.count", "{0} ancients", ancientList.GetChildCount());

        return new AncientsPanelHandle(backBtn, extStatusLabel);
    }

    private static string FormatAncientListLabel(AncientEventModel ancient) {
        var name = EventActions.GetEventDisplayName(ancient);
        var epithet = GetAncientEpithet(ancient);
        return string.IsNullOrWhiteSpace(epithet) ? name : $"{name} — {epithet}";
    }

    private static string GetAncientEpithet(AncientEventModel ancient) {
        try { return ancient.Epithet?.GetFormattedText() ?? ""; }
        catch { return ""; }
    }
}
