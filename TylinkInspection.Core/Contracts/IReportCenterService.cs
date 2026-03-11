using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IReportCenterService
{
    ReportOverview GetOverview(ReportTimeRange timeRange);
}
