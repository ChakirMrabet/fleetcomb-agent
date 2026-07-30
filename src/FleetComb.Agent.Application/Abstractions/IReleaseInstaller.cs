using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Application.Abstractions;

public interface IReleaseInstaller
{
    bool CanInstall(DesiredRelease release);
    Task<int> InstallAsync(
        DesiredRelease release,
        string artifactPath,
        CancellationToken cancellationToken);
}
