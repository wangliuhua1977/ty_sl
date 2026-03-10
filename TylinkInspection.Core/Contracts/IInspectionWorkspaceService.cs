using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IInspectionWorkspaceService
{
    InspectionWorkspaceData GetWorkspaceData();
}
