using TylinkInspection.Core.Configuration;

namespace TylinkInspection.Core.Contracts;

public interface IOpenPlatformOptionsProvider
{
    TylinkOpenPlatformOptions GetOptions();
}
