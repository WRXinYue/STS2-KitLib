using KitLib.DevPerf;

namespace KitLib.Abstractions.Tests;

public sealed class DevPerfMetricsTests {
    sealed class TestProvider(int order, params string[] lines) : IDevPerfLineProvider {
        public int Order => order;
        public void AppendLines(List<DevPerfLine> sink) {
            foreach (var line in lines)
                sink.Add(new DevPerfLine(line));
        }
    }

    public DevPerfMetricsTests() => DevPerfMetrics.ClearProvidersForTests();

    [Fact]
    public void CollectLines_SortsByOrder() {
        DevPerfMetrics.Register(new TestProvider(20, "b"));
        DevPerfMetrics.Register(new TestProvider(10, "a"));

        var lines = new List<DevPerfLine>();
        DevPerfMetrics.CollectLines(lines);

        Assert.Equal(["a", "b"], lines.Select(l => l.Text).ToArray());
    }

    [Fact]
    public void Unregister_RemovesProvider() {
        var provider = new TestProvider(0, "x");
        DevPerfMetrics.Register(provider);
        DevPerfMetrics.Unregister(provider);

        var lines = new List<DevPerfLine>();
        DevPerfMetrics.CollectLines(lines);

        Assert.Empty(lines);
    }
}
