using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.Models;

/// <summary>Debug combat room for the KitLib test server.</summary>
public sealed class KitLibTestServerEncounter : EncounterModel {
    public override RoomType RoomType => RoomType.Monster;

    public override bool ShouldGiveRewards => false;

    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        [ModelDb.Monster<KitLibTestServerDummy>()];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        [(ModelDb.Monster<KitLibTestServerDummy>().ToMutable(), null)];
}
