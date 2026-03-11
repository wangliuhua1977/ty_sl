namespace TylinkInspection.Core.Contracts;

public interface IInspectionSelectionService
{
    event EventHandler? SelectionChanged;

    string? GetSelectedDeviceCode();

    void SetSelectedDevice(string? deviceCode);
}
