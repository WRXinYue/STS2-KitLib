using KitLib.DevPerf;

namespace KitLib.Abstractions.Tests;

public sealed class DevPerfTransitionStoreTests {
    public DevPerfTransitionStoreTests() => DevPerfTransitionStore.ClearForTests();

    [Fact]
    public void Record_KeepsMostRecentEntries() {
        for (int i = 0; i < DevPerfTransitionStore.MaxRecords + 2; i++)
            DevPerfTransitionStore.Record($"evt{i}", i);

        var snapshot = DevPerfTransitionStore.Snapshot();
        Assert.Equal(DevPerfTransitionStore.MaxRecords, snapshot.Count);
        Assert.Equal("evt2", snapshot[0].Name);
        Assert.Equal("evt4", snapshot[^1].Name);
    }
}
