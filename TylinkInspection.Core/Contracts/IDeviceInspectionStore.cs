using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IDeviceInspectionStore
{
    IReadOnlyList<DeviceInspectionResult> Load();

    void Save(IReadOnlyList<DeviceInspectionResult> results);
}
