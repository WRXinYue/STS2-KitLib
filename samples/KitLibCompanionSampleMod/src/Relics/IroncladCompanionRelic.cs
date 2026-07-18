using KitLibCompanionSampleMod.Pools;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace KitLibCompanionSampleMod.Relics;

[RitsuLibOwnedBy(MainFile.ModId)]
[RegisterRelic(typeof(CompanionSampleRelicPool), FullPublicEntry = "IRONCLAD_COMPANION_RELIC")]
public sealed class IroncladCompanionRelic : ModRelicTemplate {
    public override RelicRarity Rarity => RelicRarity.None;

    public override bool HasUponPickupEffect => true;

    public override bool IsAllowed(IRunState runState) => false;

    public override bool IsAllowedInShops => false;

    public override Task AfterObtained() {
        CompanionSummonScheduler.RequestSummonFromRelic();
        return Task.CompletedTask;
    }
}
