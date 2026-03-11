using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IInspectionScopeService
{
    event EventHandler? ScopeChanged;

    IReadOnlyList<InspectionScopeScheme> GetSchemes();

    InspectionScopeScheme GetCurrentScheme();

    InspectionScopeResult GetCurrentScope();

    InspectionScopeResult GetScope(string schemeId);

    InspectionScopeScheme SaveScheme(InspectionScopeScheme scheme);

    void SetCurrentScheme(string schemeId);

    void DeleteScheme(string schemeId);

    void RefreshScope(bool forceCatalogRefresh = false);
}
