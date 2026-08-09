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
        KitLibCheatApi.EnsureRuntimeStatModifiers = () => CheatRunState.Ensure();
        KitLibCheatApi.ClearRunState = CheatRunState.ClearRunState;
        KitLibCheatApi.SetMultiplayerCheatOptIn = MpCheatSession.SetLocalOptIn;
        KitLibCheatApi.CanUseMultiplayerCheats = () => MpCheatSession.CanUseMultiplayerCheats;
        KitLibCheatApi.ProcessFrame = delta => {
            if (MpCheatApplier.CheatsActive)
                PlayerCheatEffects.Update();
            if (MpCheatApplier.FrameCheatsAllowed)
                CheatRunState.StatModifiers?.Update(delta);
        };
    }
}
