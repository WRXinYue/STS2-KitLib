using System.Reflection;
using HarmonyLib;
using KitLib.Abstractions.Compat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib;

internal static class Sts2RuntimeProfile {
    private const BindingFlags MemberFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    public static Sts2GameProfile Current { get; private set; } = Sts2GameProfile.Unknown;
    public static Sts2Platform Platform { get; private set; } = Sts2Platform.Unknown;
    public static string? RawVersion { get; private set; }
    public static bool IsSupported { get; private set; }
    public static bool SanityMismatch { get; private set; }

    public static bool AllowHighRiskModules =>
        IsSupported && !SanityMismatch && Current != Sts2GameProfile.Unknown;

    public static void Initialize() {
        RawVersion = ReleaseInfoManager.Instance.ReleaseInfo?.Version;
        Platform = DetectPlatform();
        Current = Sts2ProfileMap.Resolve(RawVersion, Platform);
        IsSupported = Current != Sts2GameProfile.Unknown;
        ValidateProfileSanity();

        MainFile.Logger.Info(
            $"STS2 profile={Current} version={RawVersion ?? "?"} platform={Platform} supported={IsSupported} sanityMismatch={SanityMismatch}");
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

    static void ValidateProfileSanity() {
        if (!IsSupported)
            return;

        switch (Current) {
            case Sts2GameProfile.StablePre106:
                if (AccessTools.Property(typeof(CombatManager), "IsPlayPhase") == null) {
                    SanityMismatch = true;
                    MainFile.Logger.Warn(
                        "STS2 profile StablePre106 but CombatManager.IsPlayPhase is missing — binary may not match profile.");
                }
                break;
            case Sts2GameProfile.Beta106Plus:
                if (AccessTools.Property(typeof(CombatManager), "IsPlayPhase") != null) {
                    SanityMismatch = true;
                    MainFile.Logger.Warn(
                        "STS2 profile Beta106Plus but CombatManager.IsPlayPhase still exists — binary may not match profile.");
                }
                if (AccessTools.Property(typeof(RunManager), "ActionQueueSynchronizer") == null) {
                    SanityMismatch = true;
                    MainFile.Logger.Warn(
                        "STS2 profile Beta106Plus but RunManager.ActionQueueSynchronizer is missing.");
                }
                break;
        }
    }
}
