namespace KitLib.Host;

/// <summary>SyncBot hooks registered by KitDevTools when the harness is loaded.</summary>
public static class KitLibSyncBotOps {
    public static Func<bool>? IsEnabled { get; set; }
    public static Action? OnRunEnded { get; set; }
    public static Action<object>? InjectPrepareAcks { get; set; }
}
