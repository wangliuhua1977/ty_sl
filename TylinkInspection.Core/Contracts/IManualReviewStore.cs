using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IManualReviewStore
{
    IReadOnlyList<ManualReviewRecord> Load();

    void Save(IReadOnlyList<ManualReviewRecord> reviews);
}
