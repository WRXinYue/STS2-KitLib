using System;

namespace KitLib.Panels;

/// <summary>
/// Callbacks consumed by built-in overlay panels (Save/Load, Settings).
/// Panel-open actions have moved to <see cref="DevPanelRegistry"/>.
/// </summary>
internal sealed class DevPanelActions {
    // Save / Load overlay (slot picker is embedded in the same browser panel)
    public required Action OnNewTest { get; init; }

    // UI coordination
    public required Action OnRefreshPanel { get; init; }

    // Settings overlay
    public required Action OnCycleGameSpeed { get; init; }
    public required Func<string> GetGameSpeedLabel { get; init; }
    public required Action OnToggleSkipAnim { get; init; }
    public required Func<string> GetSkipAnimLabel { get; init; }
}
