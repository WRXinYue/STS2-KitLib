using KitLib.Logging;

namespace KitLib.Abstractions.Tests;

[Collection("KitLibLog")]
public class KitLibLogFormatTests {
    [Fact]
    public void FormatLine_ModOnly() {
        Assert.Equal("[my-mod] loaded", KitLibLogFormat.FormatLine("my-mod", null, "loaded"));
    }

    [Fact]
    public void FormatLine_ModAndScope() {
        Assert.Equal("[my-mod][Combat] turn 3", KitLibLogFormat.FormatLine("my-mod", "Combat", "turn 3"));
    }

    [Fact]
    public void FormatLine_EmptyMessage() {
        Assert.Equal("[my-mod][Save]", KitLibLogFormat.FormatLine("my-mod", "Save", null));
    }

    [Fact]
    public void FormatLine_UnknownModWhenBlank() {
        Assert.Equal("[Unknown] ping", KitLibLogFormat.FormatLine("  ", null, "ping"));
    }

    [Fact]
    public void FormatLine_KitLibInternalShape() {
        Assert.Equal("[KitLib][MpCheat] armed", KitLibLogFormat.FormatLine("KitLib", "MpCheat", "armed"));
    }

    [Fact]
    public void FormatGameLoggerText_hostOmitsRepeatedModId() {
        Assert.Equal("loaded", KitLibLogFormat.FormatGameLoggerText("KitLib", null, "loaded"));
        Assert.Equal("[PseudoCoop] starting", KitLibLogFormat.FormatGameLoggerText("KitLib", "PseudoCoop", "starting"));
    }

    [Fact]
    public void FormatGameLoggerText_contentModKeepsFullPrefix() {
        Assert.Equal("[my-mod][Combat] turn 3", KitLibLogFormat.FormatGameLoggerText("my-mod", "Combat", "turn 3"));
    }

    [Fact]
    public void FormatGameCallbackText_hostScoped() {
        Assert.Equal("[KitLib] [PseudoCoop] starting", KitLibLogFormat.FormatGameCallbackText("KitLib", "PseudoCoop", "starting"));
    }

    [Fact]
    public void FormatCompoundSource_Scoped() {
        Assert.Equal("my-mod][Combat", KitLibLogFormat.FormatCompoundSource("my-mod", "Combat"));
    }

    [Fact]
    public void FormatCompoundSource_Unscoped() {
        Assert.Equal("my-mod", KitLibLogFormat.FormatCompoundSource("my-mod", null));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("KitLib", null)]
    [InlineData("kitlib", null)]
    [InlineData("KitLibHost", "Host")]
    [InlineData("KitLib.CombatAdd", "CombatAdd")]
    [InlineData("KitLib CrashRecovery", "CrashRecovery")]
    [InlineData("MpCheat", "MpCheat")]
    [InlineData("  AiHost  ", "AiHost")]
    public void NormalizeKitLibScope_maps_legacy_tags(string? input, string? expected) {
        Assert.Equal(expected, KitLibLogFormat.NormalizeKitLibScope(input));
    }

    [Fact]
    public void NormalizeKitLibScope_respects_custom_mod_id() {
        Assert.Null(KitLibLogFormat.NormalizeKitLibScope("my-mod", "my-mod"));
        Assert.Equal("Combat", KitLibLogFormat.NormalizeKitLibScope("Combat", "my-mod"));
    }
}

[Collection("KitLibLog")]
public class KitLibLogTests {
    [Fact]
    public void IsAvailable_FalseUntilBound() {
        Assert.False(KitLibLog.IsAvailable);
    }

    [Fact]
    public void Write_InvokesBoundWriter() {
        KitLogLevel? level = null;
        string? scope = null;
        string? message = null;

        KitLibLog.Bind((l, s, m) => {
            level = l;
            scope = s;
            message = m;
        });

        try {
            Assert.True(KitLibLog.IsAvailable);
            KitLibLog.Info("Combat", "hello");
            Assert.Equal(KitLogLevel.Info, level);
            Assert.Equal("Combat", scope);
            Assert.Equal("hello", message);
        }
        finally {
            KitLibLog.Bind(null);
        }
    }

    [Fact]
    public void Scope_RequiresNonEmptyName() {
        Assert.Throws<ArgumentException>(() => new KitLibLogScope("  "));
    }

    [Fact]
    public void Scope_ForwardsToBoundWriter() {
        string? scope = null;
        KitLibLog.Bind((_, s, _) => scope = s);
        try {
            KitLibLog.Scope("Save").Warn("bad checksum");
            Assert.Equal("Save", scope);
        }
        finally {
            KitLibLog.Bind(null);
        }
    }
}
