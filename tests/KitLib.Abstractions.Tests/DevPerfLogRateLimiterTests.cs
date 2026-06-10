using KitLib.DevPerf;

namespace KitLib.Abstractions.Tests;

public sealed class DevPerfLogRateLimiterTests {
    [Fact]
    public void ShouldLog_RespectsInterval() {
        var now = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var limiter = new DevPerfLogRateLimiter(() => now);

        Assert.True(limiter.ShouldLog("frame_spike", TimeSpan.FromSeconds(5)));
        Assert.False(limiter.ShouldLog("frame_spike", TimeSpan.FromSeconds(5)));

        now = now.AddSeconds(5);
        Assert.True(limiter.ShouldLog("frame_spike", TimeSpan.FromSeconds(5)));
    }
}
