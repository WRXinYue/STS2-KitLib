using System;
using System.Reflection;
using Godot;
using KitLib.Abstractions.Modding;

namespace KitLib.Integration;

/// <summary>
/// Keeps a hidden <c>RitsuModSettingsSubmenu</c> on the scene root so
/// <c>ModSettingsUiContext</c> can bind saves/refreshes while KitLib renders settings in its own shell.
/// </summary>
internal static class RitsuModSettingsEmbedHost {
    private const string SubmenuFullName = ModPanelEmbedHostProbe.SubmenuFullName;
    private static Control? _pin;
    private static Node? _submenu;

    public static Node? TryGetSubmenu() => HasLiveSubmenu() ? _submenu : null;

    public static void Ensure() {
        if (HasLiveSubmenu())
            return;

        if (Engine.GetMainLoop() is not SceneTree tree) {
            MainFile.Logger.Warn("KitLib ModPanel: embed host skipped (no SceneTree).");
            return;
        }

        var hostRoot = tree.Root;
        if (hostRoot == null || !GodotObject.IsInstanceValid(hostRoot)) {
            MainFile.Logger.Warn("KitLib ModPanel: embed host skipped (invalid scene root).");
            return;
        }

        var asm = RitsuModSettingsBridge.TryGetRitsuAssembly();
        if (asm == null) {
            MainFile.Logger.Warn("KitLib ModPanel: STS2-RitsuLib assembly missing for embed host.");
            return;
        }

        var submenuType = asm.GetType(SubmenuFullName);
        if (submenuType == null) {
            MainFile.Logger.Warn($"KitLib ModPanel: type not found: {SubmenuFullName}");
            return;
        }

        _pin = new Control {
            Name = "KitLibRitsuSettingsEnginePin",
            CustomMinimumSize = new Vector2(1f, 1f),
            Size = new Vector2(1f, 1f),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ProcessMode = Node.ProcessModeEnum.Always,
        };
        hostRoot.AddChild(_pin);

        var created = TryInstantiateSubmenu(submenuType);
        if (created == null) {
            MainFile.Logger.Warn($"KitLib ModPanel: failed to instantiate {SubmenuFullName}");
            _pin.QueueFree();
            _pin = null;
            return;
        }

        _submenu = created;
        _pin.AddChild(_submenu);
        MuteEmbeddedSubmenuInput(_pin);
        MuteEmbeddedSubmenuInput((Node)_submenu);
        TryInvokeEmbedDisable((Node)_submenu);
        MainFile.Logger.Info("KitLib ModPanel: RitsuModSettingsSubmenu embed host ready.");
    }

    static bool HasLiveSubmenu() {
        if (_submenu != null && GodotObject.IsInstanceValid(_submenu))
            return true;
        DropStaleRefs();
        return false;
    }

    static void DropStaleRefs() {
        if (_submenu != null && !GodotObject.IsInstanceValid(_submenu))
            _submenu = null;
        if (_pin != null && !GodotObject.IsInstanceValid(_pin))
            _pin = null;
    }

    private static void MuteEmbeddedSubmenuInput(Node node) {
        node.SetProcessInput(false);
        node.SetProcessUnhandledInput(false);
    }

    private static void TryInvokeEmbedDisable(Node submenu) {
        try {
            var disable = submenu.GetType().GetMethod("Disable",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            disable?.Invoke(submenu, null);
        }
        catch (Exception ex) {
            MainFile.Logger.Info(
                $"KitLib ModPanel: embed submenu Disable() skipped: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    private static Node? TryInstantiateSubmenu(Type submenuType) {
        try {
            if (Activator.CreateInstance(submenuType) is Node node)
                return node;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn(
                $"KitLib ModPanel: Activator.CreateInstance({submenuType.Name}) failed: {ex.InnerException?.Message ?? ex.Message}");
        }
        try {
            if (ClassDB.ClassExists(submenuType.Name)
                && ClassDB.Instantiate(submenuType.Name).AsGodotObject() is Node classDbNode)
                return classDbNode;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn(
                $"KitLib ModPanel: ClassDB.Instantiate({submenuType.Name}) failed: {ex.InnerException?.Message ?? ex.Message}");
        }
        return null;
    }

    public static void SyncSubmenuSelection(string modId, string pageId) {
        if (!HasLiveSubmenu())
            return;
        var t = _submenu!.GetType();
        t.GetField("_selectedModId", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(_submenu, modId);
        t.GetField("_selectedPageId", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(_submenu, pageId);
    }

    public static void FlushDirtyBindings() {
        if (!HasLiveSubmenu())
            return;
        var m = _submenu!.GetType().GetMethod("FlushDirtyBindings",
            BindingFlags.Instance | BindingFlags.NonPublic);
        try {
            m?.Invoke(_submenu, null);
        }
        catch {
            // ignored
        }
    }
}
