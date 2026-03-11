using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IAiInspectionTaskPlanStore
{
    IReadOnlyList<AiInspectionTaskPlan> LoadAll();

    void SaveAll(IReadOnlyList<AiInspectionTaskPlan> plans);
}
