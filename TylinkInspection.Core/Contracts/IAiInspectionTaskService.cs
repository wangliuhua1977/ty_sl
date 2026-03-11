using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IAiInspectionTaskService
{
    event EventHandler? TasksChanged;

    void Start();

    void Stop();

    AiInspectionTaskOverview GetOverview(AiInspectionTaskQuery? query = null);

    IReadOnlyList<AiInspectionTaskBatch> Query(AiInspectionTaskQuery query);

    IReadOnlyList<AiInspectionTaskPlan> GetPlans();

    IReadOnlyList<AiInspectionTaskPlanExecutionHistory> GetPlanExecutionHistory();

    IReadOnlyList<AiInspectionTaskBatch> GetPlanExecutionBatches(string planId);

    AiInspectionFailureDashboard GetFailureDashboard();

    AiInspectionTaskContextSummary? GetTaskContext(InspectionModuleNavigationContext? context);

    AiInspectionTaskBatch? GetDetail(string taskId);

    AiInspectionTaskBatch CreateTask(AiInspectionTaskCreateRequest request);

    AiInspectionTaskPlan CreatePlan(AiInspectionTaskPlanCreateRequest request);

    AiInspectionTaskPlan SetPlanEnabled(string planId, bool isEnabled, string? operatorName = null);

    AiInspectionTaskBatch StartTask(string taskId, string? operatorName = null);

    AiInspectionTaskBatch CancelTask(string taskId, string? operatorName, string? note);

    AiInspectionTaskBatch RetryTaskItem(string taskId, string itemId, string? operatorName = null);

    AiInspectionTaskBatch RerunFailedItems(string taskId, string? operatorName = null);

    AiInspectionTaskBatch RerunUnsuccessfulItems(string taskId, string? operatorName = null);
}
