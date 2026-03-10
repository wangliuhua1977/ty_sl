using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IAiInspectionCenterService
{
    IReadOnlyList<AiInspectionTaskListItem> Query(AiInspectionTaskQuery query);

    AiInspectionTaskDetail? GetDetail(string taskId);

    void UpdateStatus(AiInspectionTaskMutation mutation);

    void RetryTask(string taskId, string? note);
}
