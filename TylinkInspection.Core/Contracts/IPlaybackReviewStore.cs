using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IPlaybackReviewStore
{
    IReadOnlyList<PlaybackReviewResult> Load();

    void Save(IReadOnlyList<PlaybackReviewResult> results);
}
