using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>
/// Enemy browser and unified encounter picker for Dev panel / combat sidebar.
/// Shared creature-visual loading helpers used by map tooltips and picker previews.
/// </summary>
internal static partial class EnemySelectUI {
    private const string RootName = "KitLibEnemySelect";

    // Cache of monsters whose visuals failed to load — avoid retrying
    private static readonly HashSet<string> _failedVisuals = new();

    /// <summary>
    /// Safely try to create creature visuals. Returns null on failure.
    /// Caches failures to avoid repeated error spam.
    /// </summary>
    private static NCreatureVisuals? TryCreateVisuals(MonsterModel monster) {
        return TryCreateVisualsPublic(monster);
    }

    /// <summary>Public accessor for other patches to use safe visual loading.</summary>
    public static NCreatureVisuals? TryCreateVisualsPublic(MonsterModel monster) {
        var id = ((AbstractModel)monster).Id.Entry;
        if (_failedVisuals.Contains(id)) return null;

        try {
            // Use ToMutable().CreateVisuals() which respects any VisualsPath overrides.
            // AssetCache.GetScene will fallback to synchronous ResourceLoader.Load
            // if the scene isn't pre-cached — this is fine for preview purposes.
            return monster.ToMutable().CreateVisuals();
        }
        catch (Exception ex) {
            _failedVisuals.Add(id);
            MainFile.Logger.Warn($"EnemySelectUI: Visual load failed for {id}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Load creature visuals into a SubViewport, with fallback label on failure.
    /// Returns the list of created visuals for cleanup.
    /// </summary>
    private static List<NCreatureVisuals> LoadVisualsIntoViewport(
        SubViewport viewport, IList<MonsterModel> monsters, int maxCount = 3) {
        var result = new List<NCreatureVisuals>();
        int count = Math.Min(monsters.Count, maxCount);
        float totalWidth = viewport.Size.X;
        float spacing = totalWidth / Math.Max(count, 1);

        for (int i = 0; i < count; i++) {
            var visuals = TryCreateVisuals(monsters[i]);
            if (visuals != null) {
                float scale = count <= 1 ? 0.45f : count == 2 ? 0.35f : 0.3f;
                visuals.Scale = new Vector2(scale, scale);
                visuals.Position = new Vector2(spacing * i + spacing / 2, viewport.Size.Y * 0.75f);
                viewport.AddChild(visuals);

                // Start idle animation — _Ready() initializes SpineBody,
                // but GenerateAnimator is needed to drive the state machine.
                try {
                    if (visuals.SpineBody != null) {
                        var mutable = monsters[i].ToMutable();
                        mutable.GenerateAnimator(visuals.SpineBody);
                        visuals.SetUpSkin(mutable);
                    }
                }
                catch { /* non-critical: preview works without animation */ }

                result.Add(visuals);
            }
        }

        // If no visuals loaded at all, show a fallback label
        if (result.Count == 0 && monsters.Count > 0) {
            var fallback = new Label {
                Text = I18N.T("enemy.previewUnavailable", "Preview unavailable"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            fallback.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
            fallback.Position = new Vector2(totalWidth / 2 - 40, viewport.Size.Y / 2 - 10);
            viewport.AddChild(fallback);
        }

        return result;
    }

    /// <summary>Clear all creature visuals (and any fallback labels) from a viewport.</summary>
    private static void ClearViewport(SubViewport viewport, List<NCreatureVisuals> visuals) {
        foreach (var v in visuals)
            if (GodotObject.IsInstanceValid(v)) v.QueueFree();
        visuals.Clear();

        // Also remove any fallback labels
        foreach (var child in viewport.GetChildren())
            if (child is Label) child.QueueFree();
    }

    internal static void GrabEncounterSearchFocus(VBoxContainer vbox) {
        foreach (var child in vbox.GetChildren()) {
            if (child is not Control control)
                continue;
            if (TryGrabSearchFocus(control))
                return;
        }
    }

    private static bool TryGrabSearchFocus(Control control) {
        if (control is LineEdit search) {
            search.GrabFocus();
            return true;
        }

        foreach (var child in control.GetChildren()) {
            if (child is Control nested && TryGrabSearchFocus(nested))
                return true;
        }

        return false;
    }

    public static void Hide(NGlobalUi globalUi) {
        _activeMapSession = null;

        var dual = _mainDual;
        var extHost = _extensionHost;
        _mainDual = null;
        _mainGlobalUi = null;
        _extensionHost = null;

        if (dual != null && GodotObject.IsInstanceValid(dual.Root)) {
            dual.KillExtCloseTween();
            if (GodotObject.IsInstanceValid(dual.ExtSlot) && dual.ExtSlot.Visible) {
                dual.ExtPanel.Position = Vector2.Zero;
                dual.ExtSlot.Visible = false;
            }
        }

        if (extHost != null && GodotObject.IsInstanceValid(extHost)) {
            foreach (var child in extHost.GetChildren())
                ((Node)child).QueueFree();
        }

        ((Node)globalUi).GetNodeOrNull<Control>($"{RootName}EncounterOverlay")?.QueueFree();
        ((Node)globalUi).GetNodeOrNull<Control>($"{RootName}CombatAddOverlay")?.QueueFree();

        var node = ((Node)globalUi).GetNodeOrNull<Control>(RootName);
        if (node != null) {
            ((Node)globalUi).RemoveChild(node);
            node.QueueFree();
        }
    }

}
