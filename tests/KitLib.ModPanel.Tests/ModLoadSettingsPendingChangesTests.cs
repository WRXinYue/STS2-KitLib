using KitLib.Abstractions.Modding;

namespace KitLib.ModPanel.Tests;

public sealed class ModLoadSettingsPendingChangesTests {
    [Theory]
    [InlineData(ModEntryLoadStatus.Disabled, true, true)]
    [InlineData(ModEntryLoadStatus.Loaded, false, true)]
    [InlineData(ModEntryLoadStatus.Loaded, true, false)]
    [InlineData(ModEntryLoadStatus.Disabled, false, false)]
    [InlineData(ModEntryLoadStatus.Failed, true, false)]
    [InlineData(ModEntryLoadStatus.AddedAtRuntime, true, true)]
    [InlineData(ModEntryLoadStatus.AddedAtRuntime, false, false)]
    [InlineData(ModEntryLoadStatus.DisabledDuplicate, true, false)]
    public void EntryHasPendingRestart_matches_official_rules(ModEntryLoadStatus runtime, bool enabled,
        bool expected) {
        Assert.Equal(expected, ModLoadSettingsPendingChanges.EntryHasPendingRestart(runtime, enabled));
    }

    [Fact]
    public void AnyPendingRestart_returns_true_when_any_entry_differs() {
        var pending = ModLoadSettingsPendingChanges.AnyPendingRestart([
            (ModEntryLoadStatus.Loaded, true),
            (ModEntryLoadStatus.Disabled, true),
        ]);
        Assert.True(pending);
    }
}
