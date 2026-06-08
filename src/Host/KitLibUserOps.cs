namespace KitLib.Host;

public static class KitLibUserOps {
    public static Action? OpenLogs { get; set; }
    public static Action? OpenManual { get; set; }
    public static Func<string?>? CurrentSessionLogFileName { get; set; }
}
