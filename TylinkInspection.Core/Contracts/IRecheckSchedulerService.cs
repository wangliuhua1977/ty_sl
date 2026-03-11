using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IRecheckSchedulerService
{
    event EventHandler? OverviewChanged;

    void Start();

    void Stop();

    void Synchronize();

    RecheckQueueOverview GetOverview();

    RecheckRuleCatalog GetRuleCatalog();

    RecheckScheduleRule GetScheduleRule();

    RecheckScheduleRule SaveScheduleRule(RecheckScheduleRule rule, string operatorName);

    RecheckScheduleRule RestoreDefaultScheduleRule(string operatorName);

    RecheckScheduleRule SaveFaultTypeRule(string faultType, RecheckScheduleRule rule, string operatorName);

    void RemoveFaultTypeRule(string faultType, string operatorName);

    RecheckTaskRecord EnsureTask(
        FaultClosureRecord sourceRecord,
        string operatorName,
        RecheckScheduleRule? scheduleRule = null);

    RecheckTaskRecord TriggerTaskNow(string taskId, string operatorName);

    RecheckTaskRecord SetTaskEnabled(string taskId, bool isEnabled, string operatorName);

    RecheckTaskRecord SaveTaskRuleOverride(string taskId, RecheckScheduleRule rule, string operatorName);

    RecheckTaskRecord ClearTaskRuleOverride(string taskId, string operatorName);
}
