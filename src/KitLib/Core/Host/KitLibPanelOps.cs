using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.Host;

/// <summary>Cross-module panel coordination without Panel assembly references from Core.</summary>
public static class KitLibPanelOps {
    public static Func<NGlobalUi, bool>? TryDismissCurrent { get; set; }
    public static Action<NGlobalUi>? OnPanelAttach { get; set; }
    public static Action<NGlobalUi>? OnPanelDetach { get; set; }
    public static Action<NGlobalUi>? OnPanelSync { get; set; }

    public static NGlobalUi? CurrentGlobalUi { get; set; }

    public static bool RequestDismiss(NGlobalUi globalUi) =>
        TryDismissCurrent?.Invoke(globalUi) ?? true;

    public static Func<bool>? IsProgressLossPromptVisible { get; set; }
    public static Action? HideDevMainMenuIfVisible { get; set; }
}
