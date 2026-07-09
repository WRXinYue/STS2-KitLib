using System;
using Godot;
using KitLib.Abstractions.Modding;
using KitLib.Modding;
using KitLib.ModPanel.Icons;
using MegaCrit.Sts2.addons.mega_text;

namespace KitLib.UI;

/// <summary>Detail banner title with optional per-mod display name override.</summary>
internal sealed partial class ModPanelDetailTitleEditor : HBoxContainer {
    private readonly MegaRichTextLabel _titleLabel;
    private readonly Button _editButton;
    private readonly LineEdit _lineEdit;
    private string _modId = "";
    private ModEntrySource _source;
    private string _defaultTitle = "";
    private string _displayTitle = "";
    private bool _editing;

    public event Action<string, ModEntrySource>? TitleCommitted;

    public ModPanelDetailTitleEditor() {
        Name = "ModPanelDetailTitleEditor";
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        AddThemeConstantOverride("separation", 4);
        _titleLabel = ModPanelUI.CreateSidebarWrapLabel(22, HorizontalAlignment.Left);
        _titleLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _titleLabel.IsHorizontallyBound = false;
        _titleLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        AddChild(_titleLabel);
        _editButton = new Button {
            Name = "ModPanelTitleEditButton",
            FocusMode = FocusModeEnum.All,
            MouseFilter = MouseFilterEnum.Stop,
            Visible = false,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(26f, 26f),
            TooltipText = I18N.T("modpanel.title.edit", "Edit display name"),
        };
        ModPanelUI.ApplyDetailTitleEditButton(_editButton);
        _editButton.Icon = MdiIcon.Pencil.Texture(16, ModPanelUiPalette.RichTextMuted);
        _editButton.Pressed += EnterEditMode;
        AddChild(_editButton);
        _lineEdit = new LineEdit {
            Name = "ModPanelTitleLineEdit",
            Visible = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, DevModeFormChrome.Metrics.ValueColumnMinHeight),
        };
        DevModeFormChrome.ApplyLineEdit(_lineEdit);
        _lineEdit.TextSubmitted += _ => CommitEdit();
        _lineEdit.FocusExited += () => {
            if (_editing)
                CommitEdit();
        };
        AddChild(_lineEdit);
    }

    public void Bind(string modId, ModEntrySource source, string defaultTitle) {
        CommitEdit();
        _modId = modId ?? "";
        _source = source;
        _defaultTitle = defaultTitle ?? "";
        var display = string.IsNullOrWhiteSpace(_modId)
            ? _defaultTitle
            : ModTitleStore.Resolve(_modId, _source, _defaultTitle);
        ShowDisplayMode(display);
    }

    public void CommitEdit() {
        if (!_editing)
            return;
        _editing = false;
        if (!string.IsNullOrWhiteSpace(_modId))
            ModTitleStore.Set(_modId, _source, _lineEdit.Text, _defaultTitle);
        var display = string.IsNullOrWhiteSpace(_modId)
            ? _defaultTitle
            : ModTitleStore.Resolve(_modId, _source, _defaultTitle);
        ShowDisplayMode(display);
        if (!string.IsNullOrWhiteSpace(_modId))
            TitleCommitted?.Invoke(_modId, _source);
    }

    private void EnterEditMode() {
        if (string.IsNullOrWhiteSpace(_modId) || _editing)
            return;
        _editing = true;
        _titleLabel.Visible = false;
        _editButton.Visible = false;
        _lineEdit.Visible = true;
        _lineEdit.Text = _displayTitle;
        _lineEdit.GrabFocus();
        _lineEdit.SelectAll();
    }

    private void ShowDisplayMode(string title) {
        _editing = false;
        _displayTitle = title;
        _lineEdit.Visible = false;
        _titleLabel.Visible = true;
        _titleLabel.SetTextAutoSize(title);
        _editButton.Visible = !string.IsNullOrWhiteSpace(_modId);
    }
}
