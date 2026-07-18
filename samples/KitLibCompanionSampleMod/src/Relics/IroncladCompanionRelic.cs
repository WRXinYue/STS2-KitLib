using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace KitLibCompanionSampleMod.Relics;

public sealed class IroncladCompanionRelic : RelicModel {
    public override RelicRarity Rarity => RelicRarity.None;

    public override LocString Title => new("relics", "IRONCLAD_COMPANION_RELIC.title");

    public override bool HasUponPickupEffect => true;

    public override bool IsAllowed(IRunState runState) => false;

    public override bool IsAllowedInShops => false;

    public override Task AfterObtained() {
        CompanionSummonScheduler.RequestSummonFromRelic();
        return Task.CompletedTask;
    }
}
