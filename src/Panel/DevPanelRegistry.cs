using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Abstractions.Host;
using KitLib.Host;
using KitLib.Icons;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.Panels;

/// <summary>Central registry for DevMode rail tabs.</summary>
internal static class DevPanelRegistry {
    private static readonly List<IDevPanelTab> _tabs = new();
    private static bool _dirty = true;

    /// <summary>Register a tab. If a tab with the same <see cref="IDevPanelTab.Id"/> already exists, it is replaced.</summary>
    internal static void Register(IDevPanelTab tab) {
        if (tab == null) throw new ArgumentNullException(nameof(tab));
        _tabs.RemoveAll(t => t.Id == tab.Id);
        _tabs.Add(tab);
        _dirty = true;
        KitLibHost.RegisterTab(ToDescriptor(tab));
    }

    /// <summary>Convenience overload — register with lambdas, no need to implement <see cref="IDevPanelTab"/>.</summary>
    internal static void Register(string id, MdiIcon icon, string displayName,
        int order, DevPanelTabGroup group, Action<NGlobalUi> onActivate,
        Action<NGlobalUi>? onDeactivate = null,
        DevPanelTabKind kind = DevPanelTabKind.Cheat) {
        Register(new LambdaTab(id, icon, displayName, order, group, onActivate, onDeactivate, kind));
    }

    /// <summary>Remove a previously registered tab by id. Returns true if found.</summary>
    internal static bool Unregister(string id) {
        int removed = _tabs.RemoveAll(t => t.Id == id);
        if (removed > 0) _dirty = true;
        return removed > 0;
    }

    static KitLibTabDescriptor ToDescriptor(IDevPanelTab tab) => new() {
        Id = tab.Id,
        IconKey = tab.Icon.Name,
        DisplayName = tab.DisplayName,
        Order = tab.Order,
        Group = tab.Group == DevPanelTabGroup.Primary ? KitLibTabGroup.Primary : KitLibTabGroup.Utility,
        Kind = tab.Kind == DevPanelTabKind.Developer ? KitLibTabKind.Developer : KitLibTabKind.Cheat,
        OnActivate = ui => tab.OnActivate((NGlobalUi)ui),
        OnDeactivate = ui => tab.OnDeactivate((NGlobalUi)ui),
        OwningModuleId = KitLibModuleIds.Core,
    };

    /// <summary>Get all tabs for a given group, sorted by <see cref="IDevPanelTab.Order"/> (stable).</summary>
    internal static IReadOnlyList<IDevPanelTab> GetTabs(DevPanelTabGroup group) {
        if (_dirty) {
            _tabs.Sort((a, b) => a.Order.CompareTo(b.Order));
            _dirty = false;
        }
        return _tabs.Where(t => t.Group == group).ToList();
    }

    /// <summary>Get all registered tabs across all groups, sorted by order.</summary>
    internal static IReadOnlyList<IDevPanelTab> GetAllTabs() {
        if (_dirty) {
            _tabs.Sort((a, b) => a.Order.CompareTo(b.Order));
            _dirty = false;
        }
        return _tabs.AsReadOnly();
    }

    /// <summary>Deactivate all tabs and clear the registry.</summary>
    internal static void DeactivateAll(NGlobalUi globalUi) {
        foreach (var tab in _tabs) {
            try { tab.OnDeactivate(globalUi); }
            catch (Exception ex) { MainFile.Logger.Warn($"DevPanelRegistry: OnDeactivate({tab.Id}) failed: {ex.Message}"); }
        }
    }

    // ── Private lambda wrapper ──

    private sealed class LambdaTab : IDevPanelTab {
        public string Id { get; }
        public MdiIcon Icon { get; }
        public string DisplayName { get; }
        public int Order { get; }
        public DevPanelTabGroup Group { get; }
        public DevPanelTabKind Kind { get; }

        private readonly Action<NGlobalUi> _onActivate;
        private readonly Action<NGlobalUi>? _onDeactivate;

        public LambdaTab(string id, MdiIcon icon, string displayName,
            int order, DevPanelTabGroup group,
            Action<NGlobalUi> onActivate, Action<NGlobalUi>? onDeactivate,
            DevPanelTabKind kind = DevPanelTabKind.Cheat) {
            Id = id;
            Icon = icon;
            DisplayName = displayName;
            Order = order;
            Group = group;
            Kind = kind;
            _onActivate = onActivate;
            _onDeactivate = onDeactivate;
        }

        public void OnActivate(NGlobalUi globalUi) => _onActivate(globalUi);
        public void OnDeactivate(NGlobalUi globalUi) => _onDeactivate?.Invoke(globalUi);
    }
}
