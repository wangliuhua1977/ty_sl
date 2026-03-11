using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IReviewCenterService
{
    ReviewCenterOverview GetOverview(ReviewCenterQuery query);

    ManualReviewRecord SaveManualReview(ManualReviewSaveRequest request);
}
