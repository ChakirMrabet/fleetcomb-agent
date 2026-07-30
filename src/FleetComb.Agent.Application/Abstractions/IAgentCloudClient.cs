using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Application.Abstractions;

public interface IAgentCloudClient
{
    Task<EnrollmentClaim> ClaimAsync(
        Uri serverUrl, string code, string publicKey,
        PlatformInformation platform, CancellationToken cancellationToken);
    Task<HeartbeatResult> HeartbeatAsync(
        AgentRegistration registration, long uptimeSeconds,
        IReadOnlyList<ApplicationObservation> applications,
        CancellationToken cancellationToken);
    Task DownloadReleaseAsync(
        AgentRegistration registration, DesiredRelease release, string destination,
        IProgress<int> progress, CancellationToken cancellationToken);
}

public sealed record EnrollmentClaim(
    Guid TenantId,
    Guid AssetId,
    Guid InstallationId,
    int HeartbeatIntervalSeconds,
    DateTimeOffset ServerTime);

public sealed record HeartbeatResult(
    DateTimeOffset ServerTime,
    int NextHeartbeatSeconds,
    DesiredState DesiredState);
