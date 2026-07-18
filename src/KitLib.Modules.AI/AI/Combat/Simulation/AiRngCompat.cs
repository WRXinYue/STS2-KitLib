using MegaCrit.Sts2.Core.Entities.Rngs;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.AI.Combat.Simulation;

internal static class AiRngCompat {
    public static Rng Create(uint seed, int counter) => new(seed, counter);

    public static int GetCounter(Rng rng) => rng.Counter;

    public static uint NamedSeed(RunRngSet rngSet, RunRngType type) {
        string name = StringHelper.SnakeCase(type.ToString());
        return (uint)(rngSet.Seed + (ulong)StringHelper.GetDeterministicHashCode(name));
    }
}
