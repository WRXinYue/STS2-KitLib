using System;
using System.IO;
using System.Threading;

namespace KitLib;

/// <summary>
/// One-time migration: delete the pre-0.28 <c>mod_data/KitLib/instances/</c> tree on startup.
/// Remove this type after most users have upgraded.
/// </summary>
internal static class LegacyInstancesDirCleanup {
    public static void ScheduleOnStartup() {
        // Defer IO; synchronous deletes during mod init are unsafe on STS2 stable.
        ThreadPool.QueueUserWorkItem(_ => TryDelete());
    }

    static void TryDelete() {
        try {
            var path = Path.Combine(DataPaths.BaseDir, "instances");
            if (!Directory.Exists(path))
                return;

            Directory.Delete(path, recursive: true);
            MainFile.Logger.Info("Removed legacy mod_data/KitLib/instances/ folder.");
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"Legacy instances/ cleanup failed: {ex.Message}");
        }
    }
}
