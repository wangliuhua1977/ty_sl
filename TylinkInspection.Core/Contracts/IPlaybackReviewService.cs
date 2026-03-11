using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IPlaybackReviewService
{
    PlaybackReviewSession PrepareLiveReview(PlaybackReviewPreparationRequest request);

    PlaybackReviewSession PreparePlaybackReview(PlaybackReviewPlaybackRequest request);

    PlaybackReviewResult CompleteReview(PlaybackReviewOutcome outcome);

    PlaybackReviewResult? GetLatestResult(string deviceCode);
}
