using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IManualCoordinateService
{
    IReadOnlyList<ManualCoordinateRecord> GetAll();

    ManualCoordinateRecord? Get(string deviceCode);

    ManualCoordinateRecord Save(ManualCoordinateRecord record);

    bool Clear(string deviceCode);
}
