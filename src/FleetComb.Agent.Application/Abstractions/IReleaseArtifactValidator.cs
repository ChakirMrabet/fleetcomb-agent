using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Application.Abstractions;

public interface IReleaseArtifactValidator
{
    Task ValidateAsync(
        DesiredRelease release,
        string artifactPath,
        CancellationToken cancellationToken);
}
