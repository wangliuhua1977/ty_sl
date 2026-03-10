using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IAiAlertService
{
    ScrollQueryResult<AiAlertListItem> Query(AiAlertQuery query);

    AiAlertDetail? GetDetail(string id);

    void UpdateWorkflowStatus(string id, string workflowStatus, string? reviewNote);
}
