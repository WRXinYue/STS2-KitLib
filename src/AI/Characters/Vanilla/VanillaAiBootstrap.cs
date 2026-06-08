using KitLib.AI.AutoPlay.Strategies;
using KitLib.AI.Knowledge;
using KitLib.AI.Planning;
using KitLib.Companion;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;

namespace KitLib.AI.Characters.Vanilla;

public static class VanillaAiBootstrap {
    static readonly StrongStrategy DefaultStrategy = new();
    static bool _packsRegistered;

    public static void Register() {
        CardCatalog.Initialize();
        RelicCatalog.Initialize();
        CardMechanicIndex.Initialize();
        RelicMechanicIndex.Initialize();
        PotionMechanicIndex.Initialize();
        MonsterMechanicIndex.Initialize();
        PotionTierCatalog.EnsureLoaded();
        RegisterPacksOnce();
        MainFile.Logger.Info("[AiVanilla] Registered 5 character DeckPlan packs.");
    }

    static void RegisterPacksOnce() {
        if (_packsRegistered) return;
        _packsRegistered = true;

        RegisterCharacter<Ironclad, IroncladPack>();
        RegisterCharacter<Silent, SilentPack>();
        RegisterCharacter<Defect, DefectPack>();
        RegisterCharacter<Necrobinder, NecrobinderPack>();
        RegisterCharacter<Regent, RegentPack>();
    }

    static void RegisterCharacter<TCharacter, TPack>()
        where TCharacter : CharacterModel
        where TPack : IDeckPlanContributor, new() {
        var id = ModelDb.GetId<TCharacter>().Entry;
        if (string.IsNullOrWhiteSpace(id)) return;

        DeckPlanContributorHub.Register(new TPack());
        CompanionBridge.RegisterCharacterStrategy(id, DefaultStrategy);
    }
}
