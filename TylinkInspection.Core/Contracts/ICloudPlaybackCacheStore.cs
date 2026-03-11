using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface ICloudPlaybackCacheStore
{
    IReadOnlyList<CloudPlaybackCacheEntry> Load();

    void Save(IReadOnlyList<CloudPlaybackCacheEntry> entries);
}
