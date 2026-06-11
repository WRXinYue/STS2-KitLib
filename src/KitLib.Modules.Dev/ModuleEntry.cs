using KitLib;
using KitLib.Abstractions.Host;
using KitLib.EnemyIntent;
using KitLib.Host;
using MegaCrit.Sts2.Core.Combat;

namespace KitLib.Dev;

public static class ModuleEntry {
    public static void Initialize() {
        if (KitLibHost.IsModuleLoaded(KitLibModuleIds.Dev))
            return;

        KitLibHost.AnnounceModule(KitLibModuleIds.Dev);
        WireEnemyIntentHost();
    }

    static void WireEnemyIntentHost() {
        KitLibHost.IsMonsterIntentOverlayReady = state =>
            MonsterIntentReader.IsOverlayCombatReady((CombatState?)state);
        KitLibHost.CaptureMonsterIntentCurrent = state =>
            MonsterIntentReader.CaptureCurrent((CombatState?)state);
        KitLibHost.CaptureMonsterIntentNextTurn = state =>
            MonsterIntentReader.CaptureNextTurn((CombatState?)state);
    }
}
