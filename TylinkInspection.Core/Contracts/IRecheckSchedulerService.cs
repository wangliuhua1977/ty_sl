using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IRecheckSchedulerService
{
    event EventHandler? OverviewChanged;

    void Start();

    void Stop();

    void Synchronize();

    RecheckQueueOverview GetOverview();

    RecheckTaskRecord EnsureTask(
        FaultClosureRecord sourceRecord,
        string operatorName,
        RecheckScheduleRule? scheduleRule = null);

    RecheckTaskRecord TriggerTaskNow(string taskId, string operatorName);

    RecheckTaskRecord SetTaskEnabled(string taskId, bool isEnabled, string operatorName);
}
