using Godot;
using KitLib.DevPerf;
using KitLib.Patches;
using MegaCrit.Sts2.Core.Nodes;

namespace KitLib.DevPerf;

internal sealed class DevPerfSceneProvider : IDevPerfLineProvider {
    public int Order => 0;

    public void AppendLines(List<DevPerfLine> lines) {
        var scene = NRun.Instance != null && GodotObject.IsInstanceValid(NRun.Instance) ? "Run" : "MainMenu";
        lines.Add(new DevPerfLine($"scene: {scene}"));
    }
}

internal sealed class DevPerfAssetWarmupProvider : IDevPerfLineProvider {
    public int Order => 10;

    public void AppendLines(List<DevPerfLine> lines) {
        var warmup = GlobalUiReadyPatch.Warmup;
        if (warmup == null) {
            lines.Add(new DevPerfLine("warmup: n/a"));
            return;
        }

        if (!warmup.IsRunning && warmup.IsCompleted)
            lines.Add(new DevPerfLine("warmup: done"));
        else if (warmup.TotalJobCount <= 0)
            lines.Add(new DevPerfLine("warmup: pending"));
        else {
            var done = warmup.TotalJobCount - warmup.PendingJobCount;
            lines.Add(new DevPerfLine($"warmup: {done}/{warmup.TotalJobCount}"));
        }
    }
}

internal sealed class DevPerfTransitionProvider : IDevPerfLineProvider {
    public int Order => 20;

    public void AppendLines(List<DevPerfLine> lines) {
        foreach (var record in DevPerfTransitionStore.Snapshot())
            lines.Add(new DevPerfLine($"last {record.Name}: {record.ElapsedMs}ms"));
    }
}

internal sealed class DevPerfFrameTimeProvider : IDevPerfLineProvider {
    public int Order => 30;

    public void AppendLines(List<DevPerfLine> lines) {
        var last = DevPerfFrameTimeSampler.LastFrameMs;
        var peak = DevPerfFrameTimeSampler.WindowPeakMs;
        var alert = peak >= 100.0;
        lines.Add(new DevPerfLine($"frame: {last:F0}ms peak {peak:F0}ms", alert));
    }
}

internal static class DevPerfBuiltinProviders {
    static readonly IDevPerfLineProvider[] BuiltIn = [
        new DevPerfSceneProvider(),
        new DevPerfAssetWarmupProvider(),
        new DevPerfTransitionProvider(),
        new DevPerfFrameTimeProvider(),
    ];

    internal static void RegisterAll() {
        foreach (var provider in BuiltIn)
            DevPerfMetrics.Register(provider);
    }
}
