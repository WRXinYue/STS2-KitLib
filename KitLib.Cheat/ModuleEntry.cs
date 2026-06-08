using HarmonyLib;
using KitLib.Abstractions.Host;
using KitLib.Host;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.Cheat;

[ModInitializer(nameof(Initialize))]
public static class ModuleEntry {
    public static void Initialize() {
        KitLibHost.AnnounceModule(KitLibModuleIds.Cheat);
        MpCheatSync.Initialize();
        WireCheatDelegates();
        CheatTabRegistration.Register();

        KitLibFeaturesPatches.EnsureApplied();
        MainFile.Logger.Info("KitLib.Cheat module initialized.");
    }

    static void WireCheatDelegates() {
        KitLibCheatOps.EnsureRuntimeStatModifiers = () => {
            CheatRunState.StatModifiers ??= new RuntimeStatModifiers();
        };
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
