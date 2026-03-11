using TylinkInspection.Core.Contracts;

namespace TylinkInspection.Services;

public sealed class InspectionSelectionService : IInspectionSelectionService
{
    private readonly object _syncRoot = new();
    private string? _selectedDeviceCode;

    public event EventHandler? SelectionChanged;

    public string? GetSelectedDeviceCode()
    {
        lock (_syncRoot)
        {
            return _selectedDeviceCode;
        }
    }

    public void SetSelectedDevice(string? deviceCode)
    {
        var normalized = string.IsNullOrWhiteSpace(deviceCode)
            ? null
            : deviceCode.Trim();

        var changed = false;
        lock (_syncRoot)
        {
            if (!string.Equals(_selectedDeviceCode, normalized, StringComparison.OrdinalIgnoreCase))
            {
                _selectedDeviceCode = normalized;
                changed = true;
            }
        }

        if (changed)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
