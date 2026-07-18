using KitLib.AI;
using KitLib.AI.Sts2;
using MegaCrit.Sts2.Core.Commands;

namespace KitLib.Singleplayer.Companion;

/// <summary>
///     Scoped card selector for companion AutoPlay only. Avoids a run-long global
///     <see cref="CardSelectCmd.UseSelector" /> that would hijack the local human's choices.
/// </summary>
internal static class SpvCompanionCardSelectScope {
    static readonly AiCombatCardSelector Selector = new(AiPlayServices.StateProvider);

    internal static IDisposable Enter() => CardSelectCmd.PushSelector(Selector);
}
