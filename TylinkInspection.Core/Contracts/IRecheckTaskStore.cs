using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IRecheckTaskStore
{
    IReadOnlyList<RecheckTaskRecord> LoadTasks();

    void SaveTasks(IReadOnlyList<RecheckTaskRecord> tasks);

    IReadOnlyList<RecheckExecutionRecord> LoadExecutions();

    void SaveExecutions(IReadOnlyList<RecheckExecutionRecord> executions);

    IReadOnlyList<RecheckTaskLatestResult> LoadLatestResults();

    void SaveLatestResults(IReadOnlyList<RecheckTaskLatestResult> latestResults);
}
