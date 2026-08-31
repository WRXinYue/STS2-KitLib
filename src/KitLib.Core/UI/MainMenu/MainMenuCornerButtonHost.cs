using System;
using System.IO;
using System.Linq;
using Godot;
using KitLib.Abstractions.Modding;
using KitLib.Host;
using KitLib.Modding;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

internal static class MainMenuCornerButtonHost {
    internal const string HostNodeName = "KitLibMainMenuCornerHost";
    internal const string VisibilitySyncNodeName = "KitLibMainMenuCornerVisibilitySync";
    const float GapBelowAnchor = 8f;
    const float ButtonGap = 8f;
    const float SameColumnTolerance = 4f;
    const float InfoLabelWidth = 188f;
    const float LabelIconGap = 8f;
    const float ToggleFlyDuration = 0.25f;
    const string InfoLabelFontPath = "res://themes/kreon_regular_shared.tres";
    const string RitsuLibGroupNodeName = "RitsuLibMainMenuModSettings";
    static readonly Color InfoLabelGold = new(0.937f, 0.784f, 0.317f, 1f);
    static Tween? _flyTween;
    static string? _flyOccupiedKey;
    static Control? _flyRow;
    static float _flyStackY;
    static bool _ritsuSuppressedByOccupancy;

    static float RowWidth => InfoLabelWidth + LabelIconGap + MainMenuCornerIconButton.ButtonSize;

    public static void EnsureAttached(NMainMenu mainMenu) {
        if (!GodotObject.IsInstanceValid(mainMenu))
            return;

        InvokeMenuReady(mainMenu);

        var patchNotesButton = FindPatchNotesButton(mainMenu);
        if (patchNotesButton == null) {
            MainFile.Logger.Warn("KitLib main-menu corner host: PatchNotesButton not found.");
            return;
        }

        var host = EnsureHost(mainMenu, patchNotesButton);
        RebuildButtonsIfNeeded(mainMenu, host);
        EnsureVisibilitySynchronizer(host, mainMenu);
        SyncPlacement(mainMenu, host, patchNotesButton);
        SyncVisibility(mainMenu);
    }

    public static void RefreshActiveMainMenu() {
        try {
            var mainMenu = MegaCrit.Sts2.Core.Nodes.NGame.Instance?.MainMenu;
            if (mainMenu != null && GodotObject.IsInstanceValid(mainMenu))
                EnsureAttached(mainMenu);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib main-menu corner refresh failed: {ex.Message}");
        }
    }

    static void InvokeMenuReady(NMainMenu mainMenu) {
        foreach (var registration in KitLibMainMenuCornerButtonRegistry.GetOrderedButtons()) {
            if (registration.OnMenuReady == null)
                continue;
            try {
                registration.OnMenuReady(mainMenu);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn(
                    $"KitLib main-menu corner OnMenuReady {registration.ModId}/{registration.ButtonId} failed: {ex.Message}");
            }
        }
    }

    public static void SyncVisibility(NMainMenu mainMenu) {
        if (!GodotObject.IsInstanceValid(mainMenu))
            return;

        if (FindHost(mainMenu) is not { } host || !GodotObject.IsInstanceValid(host))
            return;

        if (!IsFlyRowActive())
            RebuildButtonsIfNeeded(mainMenu, host);

        var occupied = FindOccupiedRegistration(mainMenu);
        bool transformOccupied = WantsIconTransform(occupied);
        bool menuSurface = IsMainMenuShortcutSurfaceOpen(mainMenu);
        bool hasButtons = EnumerateIconButtons(host).Any();
        host.Visible = hasButtons && (occupied != null || menuSurface);
        host.ClipContents = false;

        SuppressSiblingShortcuts(mainMenu, occupied != null);

        ApplyRowOccupancy(mainMenu, host, occupied, transformOccupied);
        MaybeAnimateOccupancyFly(mainMenu, host, occupied, transformOccupied);
    }

    internal static bool TrySyncPlacementIfLayoutChanged(
        NMainMenu mainMenu,
        Control host,
        Control patchNotesButton,
        ref float lastSlotTop,
        ref float lastOffsetRight) {
        if (IsFlyRowActive())
            return false;

        float slotTop = HostSlotTop(mainMenu, patchNotesButton);
        float offsetRight = patchNotesButton.OffsetRight;
        if (Mathf.IsEqualApprox(slotTop, lastSlotTop) && Mathf.IsEqualApprox(offsetRight, lastOffsetRight))
            return false;

        lastSlotTop = slotTop;
        lastOffsetRight = offsetRight;
        SyncPlacement(mainMenu, host, patchNotesButton);
        return true;
    }

    static Control? FindHost(NMainMenu mainMenu) {
        if (mainMenu.GetNodeOrNull<Control>(HostNodeName) is { } direct)
            return direct;

        var patchNotes = FindPatchNotesButton(mainMenu);
        return patchNotes?.GetParent()?.GetNodeOrNull<Control>(HostNodeName);
    }

    static Control EnsureHost(NMainMenu mainMenu, Control patchNotesButton) {
        var existing = mainMenu.GetNodeOrNull<Control>(HostNodeName);
        if (existing == null) {
            var nested = patchNotesButton.GetParent()?.GetNodeOrNull<Control>(HostNodeName);
            if (nested != null) {
                nested.GetParent()?.RemoveChild(nested);
                mainMenu.AddChild(nested);
                existing = nested;
            }
        }

        if (existing != null)
            return existing;

        var host = new Control {
            Name = HostNodeName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = false,
        };
        mainMenu.AddChild(host);
        EnsureHostSiblingOrder(mainMenu, host);
        return host;
    }

    static void RebuildButtonsIfNeeded(NMainMenu mainMenu, Control host) {
        var registrations = KitLibMainMenuCornerButtonRegistry.GetOrderedButtons();
        if (HostAlreadyShows(host, registrations))
            return;

        RebuildButtons(mainMenu, host, registrations);
    }

    static bool HostAlreadyShows(
        Control host,
        IReadOnlyList<KitLibMainMenuCornerButtonRegistration> registrations) {
        var rows = host.GetChildren()
            .OfType<Control>()
            .Where(child => !child.IsQueuedForDeletion() &&
                child.Name.ToString().StartsWith("KitLibCorner_", StringComparison.Ordinal))
            .ToArray();
        if (rows.Length != registrations.Count)
            return false;

        for (var i = 0; i < rows.Length; i++) {
            if (rows[i].Name.ToString() != BuildButtonNodeName(registrations[i]))
                return false;
        }

        return true;
    }

    static void RebuildButtons(
        NMainMenu mainMenu,
        Control host,
        IReadOnlyList<KitLibMainMenuCornerButtonRegistration> registrations) {
        foreach (var node in host.GetChildren().ToArray()) {
            if (node is MainMenuCornerButtonVisibilitySync)
                continue;
            node.QueueFree();
        }
        if (registrations.Count == 0) {
            host.CustomMinimumSize = Vector2.Zero;
            return;
        }

        MainFile.Logger.Info(
            $"KitLib main-menu corner host building {registrations.Count} button(s): " +
            string.Join(", ", registrations.Select(r => $"{r.ModId}/{r.ButtonId}")));

        float y = 0f;
        foreach (var registration in registrations) {
            host.AddChild(CreateRow(mainMenu, registration, y));
            y += MainMenuCornerIconButton.ButtonSize + ButtonGap;
        }

        host.CustomMinimumSize = new(
            RowWidth,
            y > 0f ? y - ButtonGap : MainMenuCornerIconButton.ButtonSize);
    }

    static Control CreateRow(
        NMainMenu mainMenu,
        KitLibMainMenuCornerButtonRegistration registration,
        float y) {
        float height = MainMenuCornerIconButton.ButtonSize;
        var row = new Control {
            Name = BuildButtonNodeName(registration),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        LayoutRect(row, 0f, y, RowWidth, height);

        var (fallbackName, fallbackVersion) = TryGetModLabelDefaults(registration.ModId);
        row.AddChild(CreateInfoLabel(KitLibMainMenuCornerButtonRegistry.ResolveInfoLabelText(
            registration, I18N.T, fallbackName, fallbackVersion)));

        var icon = LoadIcon(registration);
        var tooltip = KitLibMainMenuCornerButtonRegistry.ResolveTooltip(registration, I18N.T);
        var button = MainMenuCornerIconButton.Create(icon, tooltip);
        button.Name = "Icon";
        LayoutRect(
            button,
            InfoLabelWidth + LabelIconGap,
            0f,
            MainMenuCornerIconButton.ButtonSize,
            height);
        button.SetMeta("kitlib_corner_reg", RegistrationKey(registration));
        button.Connect(
            NClickableControl.SignalName.Released,
            Callable.From<NClickableControl>(_ => OnButtonPressed(mainMenu, registration)));
        row.AddChild(button);
        return row;
    }

    static MegaLabel CreateInfoLabel(string text) {
        var label = new MegaLabel {
            Name = "Info",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutoSizeEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Off,
        };
        LayoutRect(label, 0f, 0f, InfoLabelWidth, MainMenuCornerIconButton.ButtonSize);
        if (ResourceLoader.Exists(InfoLabelFontPath)) {
            label.AddThemeFontOverride(
                ThemeConstants.Label.Font,
                ResourceLoader.Load<Font>(InfoLabelFontPath));
        }
        KitLibLocaleFonts.ApplyMegaLabel(label);
        label.AddThemeFontSizeOverride(ThemeConstants.Label.FontSize, 15);
        label.AddThemeColorOverride(ThemeConstants.Label.FontColor, InfoLabelGold);
        label.AddThemeColorOverride(ThemeConstants.Label.FontShadowColor, new(0f, 0f, 0f, 0.5f));
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        label.SetTextAutoSize(text);
        return label;
    }

    static void LayoutRect(Control control, float x, float y, float width, float height) {
        control.AnchorLeft = 0f;
        control.AnchorTop = 0f;
        control.AnchorRight = 0f;
        control.AnchorBottom = 0f;
        control.OffsetLeft = x;
        control.OffsetTop = y;
        control.OffsetRight = x + width;
        control.OffsetBottom = y + height;
        control.Size = new(width, height);
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
        if (IsFlyRowActive())
            return;

        float slotTop = HostSlotTop(mainMenu, patchNotesButton);
        float width = Math.Max(host.CustomMinimumSize.X, RowWidth);
        float height = Math.Max(host.CustomMinimumSize.Y, MainMenuCornerIconButton.ButtonSize);

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

        if (host.GetParent() is Node parent)
            EnsureHostSiblingOrder(parent, host);
    }

    static float HostSlotTop(NMainMenu mainMenu, Control patchNotesButton) =>
        ResolveVerticalAnchor(mainMenu, patchNotesButton).OffsetBottom + GapBelowAnchor;

    static Control ResolveVerticalAnchor(NMainMenu mainMenu, Control patchNotesButton) {
        Control anchor = patchNotesButton;
        float bottom = patchNotesButton.OffsetBottom;

        void Consider(Control? node) {
            if (node == null || !GodotObject.IsInstanceValid(node))
                return;
            if (node.Name.ToString() == HostNodeName)
                return;
            if (!node.Visible)
                return;
            if (!Mathf.IsEqualApprox(node.OffsetRight, patchNotesButton.OffsetRight, SameColumnTolerance))
                return;
            if (node.OffsetBottom <= bottom + 0.5f)
                return;
            bottom = node.OffsetBottom;
            anchor = node;
        }

        void ConsiderChildren(Node? parent) {
            if (parent == null)
                return;
            foreach (var child in parent.GetChildren()) {
                if (child is Control control)
                    Consider(control);
            }
        }

        ConsiderChildren(patchNotesButton.GetParent());
        if (patchNotesButton.GetParent() != mainMenu)
            ConsiderChildren(mainMenu);

        return anchor;
    }

    static void EnsureHostSiblingOrder(Node parent, Control host) {
        parent.MoveChild(host, Math.Max(0, parent.GetChildCount() - 1));
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

    static Control? FindPatchNotesButton(NMainMenu mainMenu) {
        foreach (var path in new[] { "%PatchNotesButton", "PatchNotesButton" }) {
            if (mainMenu.GetNodeOrNull<Control>(path) is { } found && GodotObject.IsInstanceValid(found))
                return found;
        }

        return FindControlByNameContains(mainMenu, "PatchNote");
    }

    static Control? FindControlByNameContains(Node node, string token) {
        if (node is Control control &&
            node.Name.ToString().Contains(token, StringComparison.OrdinalIgnoreCase))
            return control;

        foreach (var child in node.GetChildren()) {
            if (child is not Node childNode)
                continue;
            var match = FindControlByNameContains(childNode, token);
            if (match != null)
                return match;
        }

        return null;
    }

    static bool IsMainMenuShortcutSurfaceOpen(NMainMenu mainMenu) {
        if (!GodotObject.IsInstanceValid(mainMenu))
            return false;

        if (mainMenu.SubmenuStack.SubmenusOpen)
            return false;

        if (mainMenu.PatchNotesScreen is { } vanillaPatchNotesScreen &&
            GodotObject.IsInstanceValid(vanillaPatchNotesScreen) &&
            vanillaPatchNotesScreen.IsOpen)
            return false;

        return true;
    }

    static KitLibMainMenuCornerButtonRegistration? FindOccupiedRegistration(NMainMenu mainMenu) {
        foreach (var registration in KitLibMainMenuCornerButtonRegistry.GetOrderedButtons()) {
            if (TryEvaluateIsOpen(mainMenu, registration))
                return registration;
        }

        return null;
    }

    static bool TryEvaluateIsOpen(NMainMenu mainMenu, KitLibMainMenuCornerButtonRegistration registration) {
        if (registration.IsOpen == null)
            return false;
        try {
            return registration.IsOpen(mainMenu);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn(
                $"KitLib main-menu corner button open-state {registration.ModId}/{registration.ButtonId} failed: {ex.Message}");
            return false;
        }
    }

    static bool WantsIconTransform(KitLibMainMenuCornerButtonRegistration? registration) =>
        registration != null;

    static void SuppressSiblingShortcuts(NMainMenu mainMenu, bool occupied) {
        if (mainMenu.GetNodeOrNull<Control>(RitsuLibGroupNodeName) is { } ritsuGroup &&
            GodotObject.IsInstanceValid(ritsuGroup)) {
            if (occupied) {
                if (ritsuGroup.Visible)
                    _ritsuSuppressedByOccupancy = true;
                ritsuGroup.Visible = false;
            }
            else if (_ritsuSuppressedByOccupancy) {
                ritsuGroup.Visible = true;
                _ritsuSuppressedByOccupancy = false;
            }
        }

        var patchNotesButton = FindPatchNotesButton(mainMenu);
        if (patchNotesButton == null || !GodotObject.IsInstanceValid(patchNotesButton))
            return;

        if (mainMenu.SubmenuStack.SubmenusOpen)
            return;
        if (mainMenu.PatchNotesScreen is { IsOpen: true })
            return;

        bool shouldShow = !occupied;
        if (patchNotesButton.Visible != shouldShow)
            patchNotesButton.Visible = shouldShow;
    }

    static void ApplyRowOccupancy(
        NMainMenu mainMenu,
        Control host,
        KitLibMainMenuCornerButtonRegistration? occupied,
        bool transformOccupied) {
        var occupiedKey = occupied == null ? null : RegistrationKey(occupied);
        float y = 0f;
        foreach (var registration in KitLibMainMenuCornerButtonRegistry.GetOrderedButtons()) {
            var row = host.GetNodeOrNull<Control>(BuildButtonNodeName(registration));
            if (row == null || !GodotObject.IsInstanceValid(row)) {
                y += MainMenuCornerIconButton.ButtonSize + ButtonGap;
                continue;
            }

            var key = RegistrationKey(registration);
            bool isSelf = occupiedKey != null &&
                string.Equals(key, occupiedKey, StringComparison.OrdinalIgnoreCase);
            bool flyRow = IsFlyRow(row);
            bool visible = occupied == null
                ? TryEvaluateButtonVisibility(mainMenu, key)
                : isSelf || flyRow;
            bool showActiveIcon = transformOccupied && isSelf;

            row.Visible = visible;
            row.ZIndex = flyRow ? 1 : 0;
            if (row.GetNodeOrNull<MainMenuCornerIconButton>("Icon") is { } button) {
                button.Visible = visible;
                button.SetEnabled(visible);
            }

            if (row.GetNodeOrNull<Control>("Info") is { } label)
                label.Visible = visible && occupied == null;

            ApplyRowIcon(row, registration, active: showActiveIcon);

            if (!flyRow)
                LayoutRect(row, 0f, y, RowWidth, MainMenuCornerIconButton.ButtonSize);

            y += MainMenuCornerIconButton.ButtonSize + ButtonGap;
        }
    }

    static void ApplyRowIcon(Control row, KitLibMainMenuCornerButtonRegistration registration, bool active) {
        if (row.GetNodeOrNull<MainMenuCornerIconButton>("Icon") is not { } button)
            return;
        var texture = active ? LoadActiveIcon(registration) : LoadIcon(registration);
        button.SetIconTexture(texture);
    }

    static void MaybeAnimateOccupancyFly(
        NMainMenu mainMenu,
        Control host,
        KitLibMainMenuCornerButtonRegistration? occupied,
        bool transformOccupied) {
        var patchNotesButton = FindPatchNotesButton(mainMenu);
        if (patchNotesButton == null || !GodotObject.IsInstanceValid(patchNotesButton))
            return;

        var nextKey = transformOccupied && occupied != null ? RegistrationKey(occupied) : null;
        if (string.Equals(nextKey, _flyOccupiedKey, StringComparison.OrdinalIgnoreCase))
            return;

        var previousRow = _flyRow;
        float restY = _flyStackY;
        _flyOccupiedKey = nextKey;
        _flyTween?.Kill();
        _flyTween = null;

        if (nextKey != null && occupied != null) {
            var row = host.GetNodeOrNull<Control>(BuildButtonNodeName(occupied));
            if (row == null || !GodotObject.IsInstanceValid(row)) {
                ClearFlyRow();
                return;
            }

            _flyRow = row;
            _flyStackY = StackOffsetY(nextKey);
            StartRowFly(row, patchNotesButton.OffsetTop - host.OffsetTop, clearRowOnFinish: false);
            return;
        }

        if (previousRow != null && GodotObject.IsInstanceValid(previousRow)) {
            _flyRow = previousRow;
            StartRowFly(previousRow, restY, clearRowOnFinish: true);
        }
        else
            ClearFlyRow();
    }

    static float StackOffsetY(string key) {
        float y = 0f;
        foreach (var registration in KitLibMainMenuCornerButtonRegistry.GetOrderedButtons()) {
            if (string.Equals(RegistrationKey(registration), key, StringComparison.OrdinalIgnoreCase))
                return y;
            y += MainMenuCornerIconButton.ButtonSize + ButtonGap;
        }

        return 0f;
    }

    static void StartRowFly(Control row, float targetTop, bool clearRowOnFinish) {
        float height = MainMenuCornerIconButton.ButtonSize;
        if (Mathf.IsEqualApprox(row.OffsetTop, targetTop)) {
            LayoutRect(row, 0f, targetTop, RowWidth, height);
            if (clearRowOnFinish)
                ClearFlyRow();
            return;
        }

        _flyTween = row.CreateTween()
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        _flyTween.SetParallel();
        _flyTween.TweenProperty(row, "offset_top", targetTop, ToggleFlyDuration);
        _flyTween.TweenProperty(row, "offset_bottom", targetTop + height, ToggleFlyDuration);
        if (clearRowOnFinish)
            _flyTween.Connect(Tween.SignalName.Finished, Callable.From(ClearFlyRow), (uint)GodotObject.ConnectFlags.OneShot);
    }

    static void ClearFlyRow() {
        if (_flyRow != null && GodotObject.IsInstanceValid(_flyRow)) {
            LayoutRect(_flyRow, 0f, _flyStackY, RowWidth, MainMenuCornerIconButton.ButtonSize);
            _flyRow.ZIndex = 0;
        }

        _flyRow = null;
        _flyTween = null;
        _flyOccupiedKey = null;
        _flyStackY = 0f;
    }

    static bool IsFlyRow(Control row) =>
        _flyRow != null && GodotObject.IsInstanceValid(_flyRow) && row == _flyRow;

    static bool IsFlyRowActive() =>
        _flyRow != null && GodotObject.IsInstanceValid(_flyRow);

    static bool IsFlyTweenRunning() =>
        _flyTween != null && GodotObject.IsInstanceValid(_flyTween) && _flyTween.IsRunning();

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
        var texture = TryLoadResTexture(path);
        if (texture != null)
            return texture;

        texture = TryLoadFileTexture(registration.ModId);
        if (texture == null)
            MainFile.Logger.Warn($"KitLib main-menu corner icon missing for {registration.ModId}: {path}");
        return texture;
    }

    static Texture2D? LoadActiveIcon(KitLibMainMenuCornerButtonRegistration registration) {
        var path = KitLibMainMenuCornerButtonRegistry.ResolveActiveIconPath(registration);
        if (string.IsNullOrWhiteSpace(path))
            return LoadIcon(registration);

        var texture = TryLoadResTexture(path);
        return texture ?? LoadIcon(registration);
    }

    static Texture2D? TryLoadResTexture(string path) {
        try {
            if (ResourceLoader.Exists(path))
                return PreloadManager.Cache.GetAsset<Texture2D>(path);
        }
        catch {
            // Fall through to GD.Load / file load.
        }

        try {
            return GD.Load<Texture2D>(path);
        }
        catch {
            return null;
        }
    }

    static Texture2D? TryLoadFileTexture(string modId) {
        try {
            var kitLibDir = ModPaths.ResolveModRoot(typeof(MainFile).Assembly);
            if (string.IsNullOrEmpty(kitLibDir))
                return null;

            var productDir = KitLibHostPaths.TryResolveProductDirectory(kitLibDir, modId);
            if (string.IsNullOrEmpty(productDir))
                return null;

            var file = Path.Combine(productDir, "mod_image.png");
            if (!File.Exists(file))
                return null;

            var image = Image.LoadFromFile(file);
            if (image == null || image.GetWidth() <= 0)
                return null;
            return ImageTexture.CreateFromImage(image);
        }
        catch {
            return null;
        }
    }

    static IEnumerable<MainMenuCornerIconButton> EnumerateIconButtons(Control host) {
        foreach (var child in host.GetChildren()) {
            if (child is MainMenuCornerIconButton button)
                yield return button;
            else if (child is Control row) {
                foreach (var nested in row.GetChildren().OfType<MainMenuCornerIconButton>())
                    yield return nested;
            }
        }
    }

    static (string? Name, string? Version) TryGetModLabelDefaults(string modId) {
        try {
            foreach (var info in ModRuntime.GetOrderedLoadedMods()) {
                if (!string.Equals(info.Id, modId, StringComparison.OrdinalIgnoreCase))
                    continue;
                return (
                    string.IsNullOrWhiteSpace(info.DisplayName) ? null : info.DisplayName,
                    string.IsNullOrWhiteSpace(info.Version) ? null : info.Version);
            }
        }
        catch {
            // Catalog is optional until mods finish loading.
        }

        return (null, null);
    }

    static string BuildButtonNodeName(KitLibMainMenuCornerButtonRegistration registration) =>
        $"KitLibCorner_{SanitizeNodeToken(registration.ModId)}_{SanitizeNodeToken(registration.ButtonId)}";

    static string RegistrationKey(KitLibMainMenuCornerButtonRegistration registration) =>
        $"{registration.ModId}/{registration.ButtonId}";

    static string SanitizeNodeToken(string value) =>
        string.Concat(value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
}
