using MegaCrit.Sts2.Core.Models;

namespace KitLib.Actions;

/// <summary>Shared runtime flags for the Card Test panel.</summary>
internal static class CardTestState {
    /// <summary>True while the automated Test queue should ignore energy / star costs.</summary>
    internal static bool BypassResourceCosts => TestingActive;

    /// <summary>True while the Card Test panel is running a queued test pass.</summary>
    internal static bool TestingActive { get; set; }

    /// <summary>Combat instance of the card currently being played; used to auto-pick it in selection UIs.</summary>
    internal static CardModel? ActiveTestCard { get; set; }
}
