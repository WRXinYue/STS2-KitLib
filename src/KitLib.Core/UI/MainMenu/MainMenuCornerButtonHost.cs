using System;
using System.Linq;
using Godot;
using KitLib.Abstractions.Modding;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

internal static class MainMenuCornerButtonHost {
    internal const string HostNodeName = "KitLibMainMenuCornerHost";
    internal const string VisibilitySyncNodeName = "KitLibMainMenuCornerVisibilitySync";
    internal const string RitsuLibGroupNodeName = "RitsuLibMainMenuModSettings";
    const float GapBelowAnchor = 8f;
    const float ButtonGap = 8f;

    public static void EnsureAttached(NMainMenu mainMenu) {
        if (!GodotObject.IsInstanceValid(mainMenu))
            return;

        if (mainMenu.GetNodeOrNull<Control>("%PatchNotesButton") is not { } patchNotesButton ||
            !GodotObject.IsInstanceValid(patchNotesButton))
            return;

        var host = EnsureHost(mainMenu);
        RebuildButtons(mainMenu, host);
        SyncPlacement(mainMenu, host, patchNotesButton);
        EnsureVisibilitySynchronizer(host, mainMenu);
        SyncVisibility(mainMenu);
    }

    public static void SyncVisibility(NMainMenu mainMenu) {
        if (!GodotObject.IsInstanceValid(mainMenu))
            return;

        if (mainMenu.GetNodeOrNull<Control>(HostNodeName) is not { } host ||
            !GodotObject.IsInstanceValid(host))
            return;

        bool surfaceVisible = IsShortcutSurfaceVisible(mainMenu);
        host.Visible = surfaceVisible && host.GetChildCount() > 0;

        foreach (var child in host.GetChildren()) {
            if (child is not MainMenuCornerIconButton button || !GodotObject.IsInstanceValid(button))
                continue;

            var registrationKey = button.GetMeta("kitlib_corner_reg").AsString();
            bool visible = surfaceVisible && TryEvaluateButtonVisibility(mainMenu, registrationKey);
            button.Visible = visible;
            button.SetEnabled(visible);
        }
    }

    internal static bool TrySyncPlacementIfLayoutChanged(
        NMainMenu mainMenu,
        Control host,
        Control patchNotesButton,
        ref float lastSlotTop,
        ref float lastOffsetRight) {
        float slotTop = HostSlotTop(mainMenu, patchNotesButton);
        float offsetRight = patchNotesButton.OffsetRight;
        if (Mathf.IsEqualApprox(slotTop, lastSlotTop) && Mathf.IsEqualApprox(offsetRight, lastOffsetRight))
            return false;

        lastSlotTop = slotTop;
        lastOffsetRight = offsetRight;
        SyncPlacement(mainMenu, host, patchNotesButton);
        return true;
    }

    static Control EnsureHost(NMainMenu mainMenu) {
        if (mainMenu.GetNodeOrNull<Control>(HostNodeName) is { } existing)
            return existing;

        var host = new Control {
            Name = HostNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        mainMenu.AddChild(host);
        EnsureHostSiblingOrder(mainMenu, host);
        return host;
    }

    static void RebuildButtons(NMainMenu mainMenu, Control host) {
        foreach (var node in host.GetChildren().OfType<MainMenuCornerIconButton>().ToArray())
            node.QueueFree();

        var registrations = KitLibMainMenuCornerButtonRegistry.GetOrderedButtons();
        if (registrations.Count == 0) {
            host.CustomMinimumSize = Vector2.Zero;
            return;
        }

        float y = 0f;
        foreach (var registration in registrations) {
            var icon = LoadIcon(registration);
            var tooltip = KitLibMainMenuCornerButtonRegistry.ResolveTooltip(registration, I18N.T);
            var button = MainMenuCornerIconButton.Create(icon, tooltip);
            button.Name = BuildButtonNodeName(registration);
            button.Position = new(0f, y);
            button.SetMeta("kitlib_corner_reg", RegistrationKey(registration));
            button.Connect(
                NClickableControl.SignalName.Released,
                Callable.From<NClickableControl>(_ => OnButtonPressed(mainMenu, registration)));
            host.AddChild(button);
            y += MainMenuCornerIconButton.ButtonSize + ButtonGap;
        }

        host.CustomMinimumSize = new(
            MainMenuCornerIconButton.ButtonSize,
            y > 0f ? y - ButtonGap : MainMenuCornerIconButton.ButtonSize);
    }

    static void OnButtonPressed(NMainMenu mainMenu, KitLibMainMenuCornerButtonRegistration registration) {
        if (!GodotObject.IsInstanceValid(mainMenu))
            return;

        try {
            registration.OnPressed(mainMenu);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn(
                $"KitLib main-menu corner button {registration.ModId}/{registration.ButtonId} failed: {ex.Message}");
        }
    }

    static void SyncPlacement(NMainMenu mainMenu, Control host, Control patchNotesButton) {
        float slotTop = HostSlotTop(mainMenu, patchNotesButton);
        float width = host.CustomMinimumSize.X;
        if (width <= 0f)
            width = MainMenuCornerIconButton.ButtonSize;
        float height = host.CustomMinimumSize.Y;
        if (height <= 0f)
            height = MainMenuCornerIconButton.ButtonSize;

        host.AnchorLeft = patchNotesButton.AnchorLeft;
        host.AnchorTop = patchNotesButton.AnchorTop;
        host.AnchorRight = patchNotesButton.AnchorRight;
        host.AnchorBottom = patchNotesButton.AnchorBottom;
        host.GrowHorizontal = patchNotesButton.GrowHorizontal;
        host.GrowVertical = patchNotesButton.GrowVertical;
        host.OffsetRight = patchNotesButton.OffsetRight;
        host.OffsetLeft = host.OffsetRight - width;
        host.OffsetTop = slotTop;
        host.OffsetBottom = slotTop + height;

        EnsureHostSiblingOrder(mainMenu, host);
    }

    static float HostSlotTop(NMainMenu mainMenu, Control patchNotesButton) =>
        ResolveVerticalAnchor(mainMenu, patchNotesButton).OffsetBottom + GapBelowAnchor;

    static Control ResolveVerticalAnchor(NMainMenu mainMenu, Control patchNotesButton) {
        if (mainMenu.GetNodeOrNull<Control>(RitsuLibGroupNodeName) is { } ritsuGroup &&
            GodotObject.IsInstanceValid(ritsuGroup))
            return ritsuGroup;

        return patchNotesButton;
    }

    static void EnsureHostSiblingOrder(NMainMenu mainMenu, Control host) {
        if (mainMenu.GetNodeOrNull<Control>(RitsuLibGroupNodeName) is not { } ritsuGroup ||
            host.GetParent() is not { } parent)
            return;

        int ritsuIndex = ritsuGroup.GetIndex();
        if (host.GetIndex() <= ritsuIndex)
            parent.MoveChild(host, Math.Min(ritsuIndex + 1, parent.GetChildCount() - 1));
    }

    static void EnsureVisibilitySynchronizer(Control host, NMainMenu mainMenu) {
        if (host.GetNodeOrNull<MainMenuCornerButtonVisibilitySync>(VisibilitySyncNodeName) is { } existing) {
            existing.Configure(mainMenu, host);
            existing.ProcessPriority = 100;
            return;
        }

        var sync = new MainMenuCornerButtonVisibilitySync {
            Name = VisibilitySyncNodeName,
            ProcessPriority = 100,
        };
        sync.Configure(mainMenu, host);
        host.AddChild(sync);
    }

    static bool IsShortcutSurfaceVisible(NMainMenu mainMenu) {
        if (!GodotObject.IsInstanceValid(mainMenu))
            return false;

        if (mainMenu.SubmenuStack.SubmenusOpen)
            return false;

        if (mainMenu.GetNodeOrNull<Control>("%PatchNotesButton") is not { } patchNotesButton ||
            !GodotObject.IsInstanceValid(patchNotesButton) ||
            !patchNotesButton.Visible)
            return false;

        if (mainMenu.PatchNotesScreen is { } vanillaPatchNotesScreen &&
            GodotObject.IsInstanceValid(vanillaPatchNotesScreen) &&
            vanillaPatchNotesScreen.IsOpen)
            return false;

        return true;
    }

    static bool TryEvaluateButtonVisibility(NMainMenu mainMenu, string registrationKey) {
        foreach (var registration in KitLibMainMenuCornerButtonRegistry.GetOrderedButtons()) {
            if (!string.Equals(RegistrationKey(registration), registrationKey, StringComparison.OrdinalIgnoreCase))
                continue;
            if (registration.IsVisible == null)
                return true;
            try {
                return registration.IsVisible(mainMenu);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn(
                    $"KitLib main-menu corner button visibility {registration.ModId}/{registration.ButtonId} failed: {ex.Message}");
                return false;
            }
        }

        return true;
    }

    static Texture2D? LoadIcon(KitLibMainMenuCornerButtonRegistration registration) {
        var path = KitLibMainMenuCornerButtonRegistry.ResolveIconPath(registration);
        try {
            if (!ResourceLoader.Exists(path))
                return null;
            return PreloadManager.Cache.GetAsset<Texture2D>(path);
        }
        catch {
            try {
                return GD.Load<Texture2D>(path);
            }
            catch {
                return null;
            }
        }
    }

    static string BuildButtonNodeName(KitLibMainMenuCornerButtonRegistration registration) =>
        $"KitLibCorner_{SanitizeNodeToken(registration.ModId)}_{SanitizeNodeToken(registration.ButtonId)}";

    static string RegistrationKey(KitLibMainMenuCornerButtonRegistration registration) =>
        $"{registration.ModId}/{registration.ButtonId}";

    static string SanitizeNodeToken(string value) =>
        string.Concat(value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
}
