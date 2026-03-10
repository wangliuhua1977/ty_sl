using TylinkInspection.Core.Models;

namespace TylinkInspection.Core.Contracts;

public interface IPlatformConnectionService
{
    PlatformConnectionTestResult TestConnection();
}
