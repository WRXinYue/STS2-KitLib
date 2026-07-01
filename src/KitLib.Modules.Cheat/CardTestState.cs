using MegaCrit.Sts2.Core.Models;

namespace KitLib.Actions;

/// <summary>Shared runtime flags for the Card Test panel.</summary>
internal static class CardTestState {
    /// <summary>
    /// When true, playing cards does not consume energy or stars and resource checks always pass.
    /// Defaults on; the Card Test panel toggle can turn it off for manual play.
    /// </summary>
    internal static bool FreePlayActive { get; set; } = true;

    /// <summary>True while card test should ignore energy / star costs.</summary>
    internal static bool BypassResourceCosts => FreePlayActive || TestingActive;

    /// <summary>True while the Card Test panel is running a queued test pass.</summary>
    internal static bool TestingActive { get; set; }

    /// <summary>Combat instance of the card currently being played; used to auto-pick it in selection UIs.</summary>
    internal static CardModel? ActiveTestCard { get; set; }
}
