using MegaCrit.Sts2.Core.Entities.Rngs;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.AI.Combat.Simulation;

internal static class AiRngCompat {
    public static Rng Create(uint seed, int counter) {
        var rng = new Rng((ulong)seed);
        FastForward(rng, counter);
        return rng;
    }

    public static int GetCounter(Rng rng) => rng.ToSerializable().counter;

    static void FastForward(Rng rng, int target) {
        while (rng.ToSerializable().counter < target)
            rng.NextInt(2);
    }

    public static uint NamedSeed(RunRngSet rngSet, RunRngType type) {
        string name = StringHelper.SnakeCase(type.ToString());
        return (uint)(rngSet.Seed + (ulong)StringHelper.GetDeterministicHashCode(name));
    }
}
