using KitLibCompanionSampleMod.Relics;
using MegaCrit.Sts2.Core.Models;

namespace KitLibCompanionSampleMod.Pools;

/// <summary>Private relic pool for console registration only; not linked to any act loot tables.</summary>
public sealed class CompanionSampleRelicPool : RelicPoolModel {
    public override string EnergyColorName => "ironclad";

    protected override IEnumerable<RelicModel> GenerateAllRelics() =>
        [ModelDb.Relic<IroncladCompanionRelic>()];
}
