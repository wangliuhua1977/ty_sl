using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IAiInspectionTaskStore
{
    IReadOnlyList<AiInspectionTaskBatch> LoadAll();

    void SaveAll(IReadOnlyList<AiInspectionTaskBatch> tasks);
}
