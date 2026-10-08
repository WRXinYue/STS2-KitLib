using System.Collections.Concurrent;
using MegaCrit.Sts2.Core.Multiplayer.Transport.Steam;
using MegaCrit.Sts2.Core.Platform.Steam;
using Steamworks;

namespace KitLib.Steam;

/// <summary>
/// Steam Workshop update info for the mod panel.
/// All methods are no-ops returning null/false when Steam is not initialized,
/// so the panel stays safe on non-Steam platforms.
/// </summary>
internal static class ModWorkshopUpdateService {
    private sealed class CacheEntry {
        internal uint LocalTs;
        internal uint? OnlineTs;
        internal Task<uint?>? OnlineQuery;
    }

    private static readonly ConcurrentDictionary<ulong, CacheEntry> _cache = new();

    internal static bool SteamAvailable => SteamInitializer.Initialized;

    /// <summary>Local install/update time of the workshop item, or null when unavailable.</summary>
    internal static DateTimeOffset? GetLocalUpdateTime(ulong workshopId) {
        var entry = _cache.GetOrAdd(workshopId, static _ => new CacheEntry());
        if (entry.LocalTs != 0)
            return FromUnixTime(entry.LocalTs);
        if (!SteamAvailable)
            return null;
        try {
            if (!SteamUGC.GetItemInstallInfo(new PublishedFileId_t(workshopId), out _, out _, 256u, out var ts))
                return null;
            entry.LocalTs = ts;
            return FromUnixTime(ts);
        }
        catch {
            return null;
        }
    }

    /// <summary>Last online update time of the workshop item, queried once per session.</summary>
    internal static Task<uint?> GetOnlineUpdateTimeAsync(ulong workshopId) {
        var entry = _cache.GetOrAdd(workshopId, static _ => new CacheEntry());
        if (entry.OnlineTs.HasValue)
            return Task.FromResult(entry.OnlineTs);
        if (entry.OnlineQuery != null)
            return entry.OnlineQuery;
        var query = QueryOnlineUpdateTimeCore(entry, workshopId);
        entry.OnlineQuery = query;
        return query;
    }

    /// <summary>True when the online revision is newer than the locally installed revision.</summary>
    internal static bool IsUpdateAvailable(ulong workshopId) {
        if (!_cache.TryGetValue(workshopId, out var entry))
            return false;
        return entry.LocalTs != 0 && entry.OnlineTs.HasValue && entry.OnlineTs.Value > entry.LocalTs;
    }

    private static async Task<uint?> QueryOnlineUpdateTimeCore(CacheEntry entry, ulong workshopId) {
        if (!SteamAvailable)
            return null;
        var queryHandle = SteamUGC.CreateQueryUGCDetailsRequest(new[] { new PublishedFileId_t(workshopId) }, 1);
        try {
            using var callResult = new SteamCallResult<SteamUGCQueryCompleted_t>(
                SteamUGC.SendQueryUGCRequest(queryHandle), SteamInitializer.DisconnectToken);
            var completed = await callResult.Task;
            if (completed.m_eResult != EResult.k_EResultOK)
                return null;
            if (!SteamUGC.GetQueryUGCResult(completed.m_handle, 0, out var details))
                return null;
            entry.OnlineTs = details.m_rtimeUpdated;
            return entry.OnlineTs;
        }
        catch {
            return null;
        }
        finally {
            SteamUGC.ReleaseQueryUGCRequest(queryHandle);
        }
    }

    internal static DateTimeOffset FromUnixTime(uint unixTs)
        => DateTimeOffset.FromUnixTimeSeconds(unixTs).ToLocalTime();
}
