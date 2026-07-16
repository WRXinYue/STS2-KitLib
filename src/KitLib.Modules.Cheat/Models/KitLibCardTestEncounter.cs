using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.Models;

/// <summary>Debug combat room for KitLib card testing.</summary>
public sealed class KitLibCardTestEncounter : EncounterModel {
    public override RoomType RoomType => RoomType.Monster;

    public override bool ShouldGiveRewards => false;

    public override bool IsDebugEncounter => true;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        [ModelDb.Monster<KitLibCardTestDummy>()];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        [(ModelDb.Monster<KitLibCardTestDummy>().ToMutable(), null)];
}
