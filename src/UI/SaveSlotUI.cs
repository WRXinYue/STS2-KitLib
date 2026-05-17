using System;
using Godot;

namespace DevMode.UI;

/// <summary>Node name for <see cref="SaveSlotFullscreenShell"/> (fullscreen) or <see cref="SaveSlotPanel"/> (embedded).</summary>
internal static class SaveSlotDialogRootId {
    internal const string NodeName = "DevModeSaveSlot";
}

internal interface ISaveSlotDialogRoot {
    void HideFromFacade();
}

/// <summary>Fullscreen vs. embedded: fullscreen uses <see cref="SaveSlotFullscreenShell"/> + shared <see cref="SaveSlotPanel"/>.</summary>
internal enum SaveSlotUiHost {
    FullscreenOverlay,
    EmbeddedInDevPanel,
}

/// <summary>Opens the save-slot UI. Layout lives on <see cref="SaveSlotPanel"/>; fullscreen adds <see cref="SaveSlotFullscreenShell"/> only.</summary>
internal static class SaveSlotUI {
    public static void Show(
        Node parent,
        bool saveMode,
        Action<int> onConfirm,
        SaveSlotUiHost host = SaveSlotUiHost.FullscreenOverlay,
        Action? onEmbeddedCancel = null,
        Action? onEmbeddedAfterLoadClose = null) {
        parent.GetNodeOrNull<Control>(SaveSlotDialogRootId.NodeName)?.QueueFree();

        if (host == SaveSlotUiHost.EmbeddedInDevPanel) {
            var panel = new SaveSlotPanel(saveMode, onConfirm, embedded: true, onEmbeddedCancel, onEmbeddedAfterLoadClose);
            panel.Name = SaveSlotDialogRootId.NodeName;
            parent.AddChild(panel);
        }
        else {
            var shell = new SaveSlotFullscreenShell(saveMode, onConfirm, onEmbeddedCancel, onEmbeddedAfterLoadClose);
            shell.Name = SaveSlotDialogRootId.NodeName;
            parent.AddChild(shell);
        }
    }

    public static void Hide() {
        var tree = Engine.GetMainLoop() as SceneTree;
        var n = tree?.Root.FindChild(SaveSlotDialogRootId.NodeName, recursive: true, owned: false);
        if (n is ISaveSlotDialogRoot root)
            root.HideFromFacade();
    }

    /// <summary>Removes embedded slot UI under <paramref name="slotHost"/>.</summary>
    internal static void TearDownEmbeddedInDevPanel(Control slotHost) {
        slotHost.GetNodeOrNull<Control>(SaveSlotDialogRootId.NodeName)?.QueueFree();
    }
}
