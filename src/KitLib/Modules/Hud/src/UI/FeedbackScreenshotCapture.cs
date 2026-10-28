using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.UI;

/// <summary>Hides KitLib chrome for one frame and captures the game viewport as PNG.</summary>
internal static class FeedbackScreenshotCapture {
    internal static async Task<byte[]?> TryCapturePngAsync() {
        var hidden = HideKitLibChrome();
        try {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree == null)
                return null;

            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            var vp = tree.Root?.GetViewport();
            if (vp == null)
                return null;

            var tex = vp.GetTexture();
            if (tex == null)
                return null;

            var img = tex.GetImage();
            if (img == null || img.GetWidth() < 8 || img.GetHeight() < 8)
                return null;

            return img.SavePngToBuffer();
        }
        catch (Exception ex) {
            KitLog.Warn("Feedback", $"Screenshot failed: {ex.Message}");
            return null;
        }
        finally {
            Restore(hidden);
        }
    }

    static List<(CanvasItem Node, bool WasVisible)> HideKitLibChrome() {
        var hidden = new List<(CanvasItem, bool)>();

        void HideDirect(Node? parent) {
            if (parent == null || !GodotObject.IsInstanceValid(parent))
                return;
            foreach (var child in parent.GetChildren()) {
                if (child is not CanvasItem ci || !GodotObject.IsInstanceValid(ci))
                    continue;
                if (!ci.Name.ToString().StartsWith("KitLib", StringComparison.Ordinal))
                    continue;
                hidden.Add((ci, ci.Visible));
                ci.Visible = false;
            }
        }

        if (Engine.GetMainLoop() is SceneTree tree)
            HideDirect(tree.Root);

        HideDirect(NRun.Instance?.GlobalUi as Node);
        HideDirect(NGame.Instance);
        HideDirect(NGame.Instance?.MainMenu);

        return hidden;
    }

    static void Restore(List<(CanvasItem Node, bool WasVisible)> hidden) {
        foreach (var (node, wasVisible) in hidden) {
            if (GodotObject.IsInstanceValid(node))
                node.Visible = wasVisible;
        }
    }
}
