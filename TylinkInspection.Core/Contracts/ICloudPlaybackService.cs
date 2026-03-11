using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface ICloudPlaybackService
{
    CloudPlaybackQueryResult GetRecentFiles(string deviceCode, string deviceName, int? netTypeCode, bool forceRefresh = false, int take = 6);

    CloudPlaybackCacheEntry? GetCachedRecentFiles(string deviceCode);

    CloudPlaybackResolutionResult ResolveFileStreams(string deviceCode, string deviceName, int? netTypeCode, CloudPlaybackFile file, bool forceRefresh = false);
}
