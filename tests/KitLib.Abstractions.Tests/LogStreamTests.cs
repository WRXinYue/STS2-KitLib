using KitLib.Logging;

namespace KitLib.Abstractions.Tests;

public class LogStreamEntryTests {
    [Fact]
    public void FromKitLog_scopedHostShape() {
        var entry = LogStreamEntry.FromKitLog(KitLogLevel.Info, "KitLib", "ProgressGuard", "starting", "KitLib");
        Assert.Equal("[KitLib] [ProgressGuard] starting", entry.Text);
        Assert.Equal("KitLib", entry.Mod);
        Assert.Equal("ProgressGuard", entry.Scope);
        Assert.Equal("info", entry.Lvl);
    }

    [Fact]
    public void FromKitLog_contentModShape() {
        var entry = LogStreamEntry.FromKitLog(KitLogLevel.Warn, "my-mod", "Combat", "turn 3", "KitLib");
        Assert.Equal("[my-mod][Combat] turn 3", entry.Text);
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

public class LogStreamFiltersTests {
    [Fact]
    public void ShouldShow_respects_minLevel_text_suppress_and_hidden_source() {
        var entry = new LogStreamEntry {
            Lvl = "info",
            Text = "[my-mod] hello world",
            Mod = "my-mod",
        };

        Assert.True(LogStreamFilters.ShouldShow(entry, null));

        Assert.False(LogStreamFilters.ShouldShow(entry, new LogViewerFilterSnapshot { MinLevel = "warn" }));
        Assert.False(LogStreamFilters.ShouldShow(entry, new LogViewerFilterSnapshot { TextFilter = "missing" }));
        Assert.False(LogStreamFilters.ShouldShow(entry, new LogViewerFilterSnapshot {
            SuppressRules = [new LogViewerFilterSnapshot.SuppressRule { Pattern = "hello", Enabled = true }],
        }));
        Assert.False(LogStreamFilters.ShouldShow(entry, new LogViewerFilterSnapshot {
            HiddenSources = ["my-mod"],
        }));
    }

    [Fact]
    public void Session_boundary_always_visible() {
        var entry = new LogStreamEntry {
            Lvl = "info",
            Text = KitLogMarkers.SessionBoundaryPrefix,
            Boundary = true,
        };
        Assert.True(LogStreamFilters.ShouldShow(entry, new LogViewerFilterSnapshot { MinLevel = "error" }));
    }

    [Fact]
    public void ParseSource_uses_mod_field_then_bracket_tag() {
        var withMod = new LogStreamEntry { Text = "x", Mod = "Alpha" };
        Assert.Equal("Alpha", LogStreamFilters.ParseSource(withMod, null));

        var fromTag = new LogStreamEntry { Text = "[Beta] combat start" };
        var filter = new LogViewerFilterSnapshot { LoadedModIds = ["Beta"] };
        Assert.Equal("Beta", LogStreamFilters.ParseSource(fromTag, filter));
    }

    [Fact]
    public void WhereVisible_filters_batch() {
        var filter = new LogViewerFilterSnapshot { MinLevel = "warn" };
        var entries = new[] {
            new LogStreamEntry { Lvl = "info", Text = "a" },
            new LogStreamEntry { Lvl = "warn", Text = "b" },
            new LogStreamEntry { Lvl = "error", Text = "c" },
        };
        var visible = LogStreamFilters.WhereVisible(entries, filter);
        Assert.Equal(2, visible.Count);
        Assert.Equal("b", visible[0].Text);
        Assert.Equal("c", visible[1].Text);
    }
}
