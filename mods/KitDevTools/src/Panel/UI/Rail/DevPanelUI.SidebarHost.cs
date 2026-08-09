using System;
using System.Collections.Generic;
using Godot;

namespace KitLib.UI;

/// <summary>Swappable content for a <see cref="DevPanelSidebarHost"/> pane.</summary>
internal interface IDevPanelSidebarProvider {
    Control Root { get; }
    string Title { get; }
    string Hint { get; }
    bool HasContent { get; }
    void Refresh();
}

/// <summary>Title + hint + content slot for a permanent panel sidebar.</summary>
internal sealed partial class DevPanelSidebarHost : VBoxContainer {
    private readonly Label _title;
    private readonly Label _hint;
    private readonly VBoxContainer _content;
    private readonly Dictionary<string, IDevPanelSidebarProvider> _providers = new();
    private readonly List<string> _activeIds = new();
    private readonly bool _railCompact;

    public DevPanelSidebarHost(string name, bool railCompact = false) {
        Name = name;
        _railCompact = railCompact;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", railCompact ? 0 : 8);

        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", railCompact ? 9 : 12);
        _title.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        _title.Visible = !railCompact;
        AddChild(_title);

        _hint = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _hint.AddThemeFontSizeOverride("font_size", 10);
        _hint.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        _hint.Visible = !railCompact;
        AddChild(_hint);

        _content = new VBoxContainer {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _content.AddThemeConstantOverride("separation", railCompact ? 2 : 8);
        AddChild(_content);
    }

    public void Register(string id, IDevPanelSidebarProvider provider) {
        _providers[id] = provider;
    }

    public void SetActive(string id) => SetActiveMany(id);

    public void SetActiveMany(params string[] ids) {
        var nextActive = new List<string>(ids.Length);
        foreach (string id in ids) {
            if (_providers.ContainsKey(id))
                nextActive.Add(id);
        }
        if (nextActive.Count == 0)
            return;

        string nextActiveKey = string.Join(',', nextActive);
        bool activeChanged = nextActiveKey != _lastActiveIdsKey;
        _activeIds.Clear();
        _activeIds.AddRange(nextActive);

        if (activeChanged) {
            _lastActiveIdsKey = nextActiveKey;
            _lastMountedKey = "";
        }

        MountActiveProvidersIfChanged();
        RefreshChrome();
    }

    public void RefreshChrome() {
        if (_activeIds.Count == 0)
            return;
        if (!_providers.TryGetValue(_activeIds[0], out var active))
            return;
        if (!_railCompact) {
            _title.Text = active.Title;
            _hint.Text = active.Hint;
        }
        else {
            _title.Text = "";
            _hint.Text = "";
        }
    }

    public void RefreshActive() {
        foreach (string id in _activeIds) {
            if (_providers.TryGetValue(id, out var provider))
                provider.Refresh();
        }
        MountActiveProvidersIfChanged();
        RefreshChrome();
    }

    public void RefreshProviders(params string[] ids) {
        foreach (string id in ids) {
            if (!_activeIds.Contains(id))
                continue;
            if (_providers.TryGetValue(id, out var provider))
                provider.Refresh();
        }
        MountActiveProvidersIfChanged();
    }

    public bool ActiveHasContent {
        get {
            foreach (string id in _activeIds) {
                if (_providers.TryGetValue(id, out var provider) && provider.HasContent)
                    return true;
            }
            return false;
        }
    }

    public IReadOnlyList<string> ActiveIds => _activeIds;

    private string _lastActiveIdsKey = "";
    private string _lastMountedKey = "";

    private void MountActiveProvidersIfChanged() {
        string key = BuildVisibleMountKey();
        if (key == _lastMountedKey)
            return;
        _lastMountedKey = key;
        MountActiveProviders();
    }

    private string BuildVisibleMountKey() {
        var parts = new List<string>(_activeIds.Count);
        foreach (string id in _activeIds) {
            if (_providers.TryGetValue(id, out var provider))
                parts.Add($"{id}:{provider.HasContent}");
        }
        return string.Join(',', parts);
    }

    private void MountActiveProviders() {
        int visibleIndex = 0;
        int visibleCount = CountVisibleProviders();

        foreach (string id in _activeIds) {
            if (!_providers.TryGetValue(id, out var provider))
                continue;

            var root = provider.Root;
            bool visible = provider.HasContent;

            if (root.GetParent() != _content)
                _content.AddChild(root);

            root.Visible = visible;
            if (!visible)
                continue;

            if (root.GetIndex() != visibleIndex)
                _content.MoveChild(root, visibleIndex);

            root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            root.SizeFlagsVertical = visibleIndex < visibleCount - 1
                ? SizeFlags.ShrinkBegin
                : SizeFlags.ExpandFill;
            visibleIndex++;
        }

        foreach (var child in _content.GetChildren()) {
            if (child is not Control control)
                continue;
            bool stillActive = false;
            foreach (string id in _activeIds) {
                if (_providers.TryGetValue(id, out var provider) && provider.Root == control) {
                    stillActive = true;
                    break;
                }
            }
            if (!stillActive)
                _content.RemoveChild(control);
        }
    }

    private int CountVisibleProviders() {
        int count = 0;
        foreach (string id in _activeIds) {
            if (_providers.TryGetValue(id, out var provider) && provider.HasContent)
                count++;
        }
        return count;
    }
}
