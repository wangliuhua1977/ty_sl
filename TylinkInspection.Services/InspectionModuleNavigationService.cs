using TylinkInspection.Core.Contracts;
using TylinkInspection.Core.Models;

namespace TylinkInspection.Services;

public sealed class InspectionModuleNavigationService : IInspectionModuleNavigationService
{
    private readonly object _syncRoot = new();
    private InspectionModuleNavigationContext? _currentContext;

    public event EventHandler<InspectionModuleNavigationRequestEventArgs>? NavigationRequested;

    public InspectionModuleNavigationContext? GetCurrentContext()
    {
        lock (_syncRoot)
        {
            return _currentContext;
        }
    }

    public void Navigate(InspectionModuleNavigationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        lock (_syncRoot)
        {
            _currentContext = context;
        }

        NavigationRequested?.Invoke(this, new InspectionModuleNavigationRequestEventArgs
        {
            Context = context
        });
    }
}
