using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IFaultClosureStore
{
    IReadOnlyList<FaultClosureRecord> Load();

    void Save(IReadOnlyList<FaultClosureRecord> records);
}
