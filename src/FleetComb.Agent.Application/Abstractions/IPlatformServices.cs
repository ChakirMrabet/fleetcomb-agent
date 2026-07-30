using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Application.Abstractions;

public interface IPlatformInformationProvider
{
    PlatformInformation Current();
}

public interface IAgentIdentityProvider
{
    (string PublicKey, string PrivateKey) Create();
    string CreateLocalApiToken();
}

public interface IReleaseInstaller
{
    bool CanInstall(DesiredRelease release);
    Task<int> InstallAsync(
        DesiredRelease release, string artifactPath, CancellationToken cancellationToken);
}

public interface IAgentDataReset
{
    Task<string> ResetAsync(CancellationToken cancellationToken);
}
