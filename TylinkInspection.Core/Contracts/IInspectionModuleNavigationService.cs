using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IInspectionModuleNavigationService
{
    event EventHandler<InspectionModuleNavigationRequestEventArgs>? NavigationRequested;

    InspectionModuleNavigationContext? GetCurrentContext();

    void Navigate(InspectionModuleNavigationContext context);
}
