using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IAiInspectionTaskStore
{
    IReadOnlyList<AiInspectionTaskDetail> LoadAll();

    void SaveAll(IReadOnlyList<AiInspectionTaskDetail> tasks);
}
