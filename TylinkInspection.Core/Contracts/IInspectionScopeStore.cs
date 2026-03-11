using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IInspectionScopeStore
{
    InspectionScopeState Load();

    void Save(InspectionScopeState state);
}
