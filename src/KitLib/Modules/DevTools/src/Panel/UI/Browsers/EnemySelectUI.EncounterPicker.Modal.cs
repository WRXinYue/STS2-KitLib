using System;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.UI;

internal static partial class EnemySelectUI {
    internal static void ShowCombatAddPickerModal(
        NGlobalUi globalUi,
        RoomType? filter,
        Action<EncounterModel> onEncounterSelected,
        Action<MonsterModel> onMonsterSelected) {
        ShowPickerModal(
            globalUi,
            $"{RootName}CombatAddOverlay",
            CombatAddPanelWidth,
            CombatAddPanelHeight,
            contentMargin: 10,
            vboxSeparation: CombatAddLayout.VboxSeparation,
            (vbox, closeModal) => BuildCombatAddPicker(
                vbox,
                filter,
                onEncounterSelected,
                onMonsterSelected,
                nextFilter => {
                    closeModal();
                    ShowCombatAddPickerModal(globalUi, nextFilter, onEncounterSelected, onMonsterSelected);
                }));
    }

    internal static void ShowEncounterPickerModal(
        NGlobalUi globalUi,
        RoomType? filter,
        Action<EncounterModel> onEncounterSelected,
        EncounterPickerOptions? options = null) {
        options ??= new EncounterPickerOptions();

        ShowPickerModal(
            globalUi,
            $"{RootName}EncounterOverlay",
            ModalDefaultWidth,
            ModalDefaultHeight,
            contentMargin: 12,
            vboxSeparation: RunRuleLayout.VboxSeparation,
            (vbox, closeModal) => {
                var builderOptions = new EncounterPickerOptions {
                    CloseOnSelect = options.CloseOnSelect,
                    ShowTitle = options.ShowTitle,
                    PickerTitle = options.PickerTitle,
                    Purpose = options.Purpose,
                    OnMonsterSelected = options.OnMonsterSelected,
                    OnFilterChanged = nextFilter => {
                        closeModal();
                        if (options.OnFilterChanged != null)
                            options.OnFilterChanged(nextFilter);
                        else
                            ShowEncounterPickerModal(globalUi, nextFilter, onEncounterSelected, options);
                    },
                };

                BuildUnifiedEncounterPicker(
                    vbox,
                    filter,
                    enc => {
                        onEncounterSelected(enc);
                        if (options.CloseOnSelect)
                            closeModal();
                    },
                    mon => options.OnMonsterSelected?.Invoke(mon),
                    builderOptions);
            });
    }

    private static void ShowPickerModal(
        NGlobalUi globalUi,
        string overlayName,
        float width,
        float height,
        int contentMargin,
        int vboxSeparation,
        Action<VBoxContainer, Action> buildContent) {
        ((Node)globalUi).GetNodeOrNull<Control>(overlayName)?.QueueFree();

        var backdrop = new ColorRect {
            Name = overlayName,
            Color = new Color(0f, 0f, 0f, 0.45f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            AnchorRight = 1,
            AnchorBottom = 1,
            ZIndex = 1300,
        };
        void CloseModal() => backdrop.QueueFree();
        backdrop.GuiInput += ev => {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                CloseModal();
        };

        float halfW = width * 0.5f;
        float halfH = height * 0.5f;
        var panel = new PanelContainer {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -halfW,
            OffsetRight = halfW,
            OffsetTop = -halfH,
            OffsetBottom = halfH,
            CustomMinimumSize = new Vector2(width, height),
            ClipContents = true,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = KitLibTheme.PanelBg,
            BorderColor = KitLibTheme.PanelBorder,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = contentMargin,
            ContentMarginRight = contentMargin,
            ContentMarginTop = contentMargin,
            ContentMarginBottom = contentMargin,
        });

        var vbox = new VBoxContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        vbox.AddThemeConstantOverride("separation", vboxSeparation);
        panel.AddChild(vbox);
        backdrop.AddChild(panel);

        buildContent(vbox, CloseModal);

        ((Node)globalUi).AddChild(backdrop);
        GrabEncounterSearchFocus(vbox);
    }

    private static void BindPickerListRegionLayout(VBoxContainer vbox, Control listRegion) {
        void Refresh() {
            if (!GodotObject.IsInstanceValid(vbox) || !GodotObject.IsInstanceValid(listRegion))
                return;

            var total = vbox.Size.Y;
            if (total <= 1f)
                return;

            var sep = (float)vbox.GetThemeConstant("separation");
            var regionIndex = listRegion.GetIndex();
            var consumed = 0f;
            for (var i = 0; i < vbox.GetChildCount(); i++) {
                if (i == regionIndex)
                    continue;
                if (vbox.GetChild(i) is not Control sibling || !sibling.Visible)
                    continue;
                consumed += Mathf.Max(sibling.Size.Y, sibling.GetCombinedMinimumSize().Y) + sep;
            }

            listRegion.CustomMinimumSize = new Vector2(0, Mathf.Max(60f, total - consumed));
        }

        vbox.Resized += Refresh;
        foreach (var child in vbox.GetChildren()) {
            if (child is not Control c || child == listRegion)
                continue;
            c.Resized += Refresh;
            c.VisibilityChanged += Refresh;
        }

        Callable.From(Refresh).CallDeferred();
        Callable.From(Refresh).CallDeferred();
    }
}
