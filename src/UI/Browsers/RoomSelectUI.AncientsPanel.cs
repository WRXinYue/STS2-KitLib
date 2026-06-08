using System;
using System.Linq;
using KitLib.Actions;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static partial class RoomSelectUI {
    private enum AncientsExtView {
        List,
        Options,
    }

    internal sealed class AncientsPanelHandle {
        internal required Button BackButton { get; init; }
        internal required Label StatusLabel { get; init; }
        internal required Action OnBackPressed { get; init; }
        internal required Action ResetToList { get; init; }
    }

    private static AncientsPanelHandle BuildAncientsPanel(
        DevPanelUI.DualColumnOverlayHandle dual,
        Label warnLabel,
        NGlobalUi globalUi) {
        var extVbox = dual.ExtContent;
        var backBtn = BuildAncientExtensionHeader(extVbox, out var titleBtn);

        var bodyHost = new VBoxContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        bodyHost.AddThemeConstantOverride("separation", 3);
        extVbox.AddChild(bodyHost);

        var extStatusLabel = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center };
        extStatusLabel.AddThemeFontSizeOverride("font_size", 11);
        extStatusLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        extVbox.AddChild(extStatusLabel);

        var view = AncientsExtView.List;

        void ShowList() {
            view = AncientsExtView.List;
            titleBtn.Text = I18N.T("room.section.ancients", "Ancient Ones");

            foreach (var child in bodyHost.GetChildren())
                ((Node)child).QueueFree();

            var ancientScroll = new ScrollContainer {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            };
            var ancientList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            ancientList.AddThemeConstantOverride("separation", 3);
            ancientScroll.AddChild(ancientList);
            bodyHost.AddChild(ancientScroll);

            foreach (var ancient in EventActions.GetAllAncients().OrderBy(EventActions.GetEventDisplayName)) {
                var captured = ancient;
                var btn = DevPanelUI.CreateListItemButton(FormatAncientListLabel(captured));
                btn.Alignment = HorizontalAlignment.Left;
                var epithet = GetAncientEpithet(captured);
                if (!string.IsNullOrWhiteSpace(epithet))
                    btn.TooltipText = epithet;
                btn.Pressed += () => OnAncientSelected(captured);
                ancientList.AddChild(btn);
            }

            extStatusLabel.Text = I18N.T("room.ancients.count", "{0} ancients", ancientList.GetChildCount());
        }

        void ShowOptionsPicker(AncientEventModel ancient) {
            view = AncientsExtView.Options;
            var name = EventActions.GetEventDisplayName(ancient);
            titleBtn.Text = I18N.T("ancient.options.title", "{0} — pin option", name);

            foreach (var child in bodyHost.GetChildren())
                ((Node)child).QueueFree();

            var scroll = new ScrollContainer {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            };
            var choicesHost = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            choicesHost.AddThemeConstantOverride("separation", 3);
            scroll.AddChild(choicesHost);
            bodyHost.AddChild(scroll);

            Callable.From(() => {
                AncientEventEnterUI.PopulateChoices(ancient, choicesHost, request => {
                    bool ok = EventActions.TryForceEnterEvent(ancient, request);
                    if (ok) {
                        RoomSelectUI.RequestClose(globalUi);
                        return;
                    }

                    extStatusLabel.Text = I18N.T("room.error", "Failed to enter room.");
                });
            }).CallDeferred();

            extStatusLabel.Text = "";
        }

        void OnAncientSelected(AncientEventModel ancient) {
            if (!RoomActions.IsRunInProgress) {
                warnLabel.Visible = true;
                extStatusLabel.Text = "";
                return;
            }

            warnLabel.Visible = false;
            if (AncientEventActions.NeedsOptionPicker(ancient)) {
                ShowOptionsPicker(ancient);
                return;
            }

            if (EventActions.TryForceEnterEvent(ancient))
                RoomSelectUI.RequestClose(globalUi);
            else
                extStatusLabel.Text = I18N.T("room.error", "Failed to enter room.");
        }

        void OnBackPressed() {
            if (view == AncientsExtView.Options) {
                ShowList();
                return;
            }

            dual.CloseExtension();
        }

        ShowList();

        return new AncientsPanelHandle {
            BackButton = backBtn,
            StatusLabel = extStatusLabel,
            OnBackPressed = OnBackPressed,
            ResetToList = ShowList,
        };
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
