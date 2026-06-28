using KitLib;
using KitLib.Abstractions.Host;
using KitLib.Host;
using KitLib.Multiplayer.Cheat;

namespace KitLib.Cheat;

public static class ModuleEntry {
    public static void Initialize() {
        if (KitLibHost.IsModuleLoaded(KitLibModuleIds.Cheat)) return;
        KitLibHost.AnnounceModule(KitLibModuleIds.Cheat);
        MpCheatSync.Initialize();
        WireCheatDelegates();
        CheatTabRegistration.Register();

        KitLibHarmony.Apply(typeof(ModuleEntry).Assembly, KitLibModuleIds.Cheat);
        MainFile.Logger.Info("KitLib.Cheat module initialized.");
    }

    static void WireCheatDelegates() {
        KitLibCheatOps.EnsureRuntimeStatModifiers = () => CheatRunState.Ensure();
        KitLibCheatOps.ClearRunState = CheatRunState.ClearRunState;
        KitLibCheatOps.SetMultiplayerCheatOptIn = MpCheatSession.SetLocalOptIn;
        KitLibCheatOps.CanUseMultiplayerCheats = () => MpCheatSession.CanUseMultiplayerCheats;
        KitLibCheatOps.ProcessFrame = delta => {
            if (MpCheatApplier.CheatsActive)
                PlayerCheatEffects.Update();
            if (MpCheatApplier.FrameCheatsAllowed)
                CheatRunState.StatModifiers?.Update(delta);
        };
    }
}
