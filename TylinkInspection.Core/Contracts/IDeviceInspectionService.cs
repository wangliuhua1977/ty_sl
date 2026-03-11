using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IDeviceInspectionService
{
    DeviceInspectionResult Inspect(DevicePointProfile profile);

    DeviceInspectionResult Inspect(InspectionScopeDevice scopeDevice);

    DeviceInspectionResult? GetLatestResult(string deviceCode);

    IReadOnlyDictionary<string, DeviceInspectionResult> GetLatestResults(IEnumerable<string> deviceCodes);
}
