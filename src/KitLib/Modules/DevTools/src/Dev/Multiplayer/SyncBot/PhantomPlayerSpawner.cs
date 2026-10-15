using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.SyncBot;

/// <summary>
/// Disabled: the phantom NetId 1001 spawn relied on the KitAI companion module
/// (removed 2026-09). Kept as a stub for a future re-enable.
/// </summary>
internal static class PhantomPlayerSpawner {
    public static bool TrySpawn(RunState? state) => false;
}
