namespace KitLib;

using KitLib.Abstractions.Compat;
using MegaCrit.Sts2.Core.Debug;

internal static class Sts2RuntimeProfile {
    public static Sts2GameProfile Current { get; private set; } = Sts2GameProfile.Unknown;
    public static Sts2Platform Platform { get; private set; } = Sts2Platform.Unknown;
    public static string? RawVersion { get; private set; }
    public static bool IsSupported { get; private set; }

    public static bool AllowHighRiskModules => IsSupported;

    public static void Initialize() {
        RawVersion = ReleaseInfoManager.Instance.ReleaseInfo?.Version;
        Platform = DetectPlatform();
        Current = Sts2ProfileMap.Resolve(RawVersion, Platform);
        IsSupported = Current != Sts2GameProfile.Unknown;

        MainFile.Logger.Info(
            $"STS2 profile={Current} version={RawVersion ?? "?"} platform={Platform} supported={IsSupported}");
    }

    static Sts2Platform DetectPlatform() {
        if (OperatingSystem.IsAndroid())
            return Sts2Platform.Android;
        if (OperatingSystem.IsWindows())
            return Sts2Platform.Windows;
        if (OperatingSystem.IsMacOS())
            return Sts2Platform.macOS;
        if (OperatingSystem.IsLinux())
            return Sts2Platform.Linux;
        return Sts2Platform.Unknown;
    }
}
