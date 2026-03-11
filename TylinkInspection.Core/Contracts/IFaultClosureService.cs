using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IFaultClosureService
{
    event EventHandler? OverviewChanged;

    FaultClosureOverview GetOverview(FaultClosureQuery query);

    FaultClosureRecord UpsertFromManualReview(ManualReviewRecord review);

    FaultClosureRecord MarkDispatched(string recordId, string operatorName, string noteText);

    FaultClosureRecord RunRecheck(string recordId, string operatorName);

    FaultClosureRecord ClearRecovered(string recordId, string operatorName, string noteText);

    FaultClosureRecord CloseRecord(string recordId, string operatorName, string noteText);

    FaultClosureRecord CloseAsFalsePositive(string recordId, string operatorName, string noteText);
}
