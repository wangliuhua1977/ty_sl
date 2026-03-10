using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IAiAlertStore
{
    IReadOnlyList<AiAlertDetail> LoadAll();

    void SaveAll(IReadOnlyList<AiAlertDetail> alerts);
}
