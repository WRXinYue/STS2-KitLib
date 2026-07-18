using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace KitLibCompanionSampleMod.Pools;

/// <summary>Private relic pool for console registration only; not linked to any act loot tables.</summary>
[RitsuLibOwnedBy(MainFile.ModId)]
public sealed class CompanionSampleRelicPool : TypeListRelicPoolModel {
    public override string EnergyColorName => "ironclad";
}
