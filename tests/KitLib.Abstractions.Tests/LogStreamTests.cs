using KitLib.Logging;

namespace KitLib.Abstractions.Tests;

public class LogStreamEntryTests {
    [Fact]
    public void FromKitLog_scopedHostShape() {
        var entry = LogStreamEntry.FromKitLog(KitLogLevel.Info, "KitLib", "PseudoCoop", "starting", "KitLib");
        Assert.Equal("[KitLib] [PseudoCoop] starting", entry.Text);
        Assert.Equal("KitLib", entry.Mod);
        Assert.Equal("PseudoCoop", entry.Scope);
        Assert.Equal("info", entry.Lvl);
    }

    [Fact]
    public void FromKitLog_contentModShape() {
        var entry = LogStreamEntry.FromKitLog(KitLogLevel.Warn, "my-mod", "Combat", "turn 3", "KitLib");
        Assert.Equal("[KitLib] [my-mod][Combat] turn 3", entry.Text);
        Assert.Equal("my-mod", entry.Mod);
        Assert.Equal("Combat", entry.Scope);
        Assert.Equal("warn", entry.Lvl);
    }

    [Fact]
    public void Json_roundTrip() {
        var original = LogStreamEntry.FromKitLog(KitLogLevel.Error, "KitLib", "Host", "boom");
        var frame = LogStreamFraming.Encode(original);
        using var ms = new MemoryStream(frame);
        Assert.True(LogStreamFraming.TryReadFrame(ms, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(original.Text, parsed!.Text);
        Assert.Equal(original.Mod, parsed.Mod);
        Assert.Equal(original.Scope, parsed.Scope);
        Assert.Equal(original.Lvl, parsed.Lvl);
    }
}

public class LogStreamFramingTests {
    [Fact]
    public void Encode_rejectsOversizedFrame() {
        var huge = new LogStreamEntry {
            Text = new string('x', LogStreamContract.MaxFrameBytes),
        };
        Assert.Throws<InvalidOperationException>(() => LogStreamFraming.Encode(huge));
    }
}

public class StructuredLogDedupeTests {
    [Fact]
    public void TryConsume_removesMarkedFingerprint() {
        StructuredLogDedupe.Clear();
        StructuredLogDedupe.Mark("info|hello");
        Assert.True(StructuredLogDedupe.TryConsume("info|hello"));
        Assert.False(StructuredLogDedupe.TryConsume("info|hello"));
    }
}

public class LogStreamHubTests {
    [Fact]
    public void Publish_replaysInHistorySnapshot() {
        LogStreamHub.Clear();
        var entry = LogStreamEntry.FromKitLog(KitLogLevel.Info, "KitLib", null, "ping");
        LogStreamHub.Publish(entry);
        var snapshot = LogStreamHub.GetHistorySnapshot();
        Assert.Single(snapshot);
        Assert.Contains("ping", snapshot[0].Text, StringComparison.Ordinal);
    }
}
