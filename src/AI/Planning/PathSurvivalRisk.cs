using System;
using MegaCrit.Sts2.Core.Map;

namespace KitLib.AI.Planning;

public static class PathSurvivalRisk {
    public static int Compute(
        MapCoord coord,
        MapSurvivalIndex index,
        DeckMetrics metrics,
        float hpRatio,
        int ascension) {
        var segment = index.GetOrDefault(coord);
        return Compute(segment.CombatsToRest, segment.ElitesToRest, metrics, hpRatio);
    }

    public static int Compute(
        float combatsToRest,
        int elitesToRest,
        DeckMetrics metrics,
        float hpRatio) {
        var blockFactor = Math.Clamp(1f - metrics.BlockDeficit * 0.25f, 0.25f, 1f);
        var drawFactor = Math.Clamp(1f - metrics.DrawDeficit * 0.15f, 0.4f, 1f);
        var hpFactor = hpRatio < 0.5f ? 1.4f : hpRatio < 0.7f ? 1.15f : 1f;

        return (int)Math.Round(
            combatsToRest * 10f * blockFactor * drawFactor * hpFactor
            + elitesToRest * 12f * hpFactor);
    }
}
