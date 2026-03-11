using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IManualCoordinateStore
{
    IReadOnlyList<ManualCoordinateRecord> Load();

    void Save(IReadOnlyList<ManualCoordinateRecord> records);
}
