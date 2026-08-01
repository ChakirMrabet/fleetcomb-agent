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
        IReadOnlyList<ProducerMessage> producerMessages,
        CancellationToken cancellationToken);
    Task DownloadReleaseAsync(
        AgentRegistration registration, DesiredRelease release, string destination,
        IProgress<int> progress, CancellationToken cancellationToken);
    Task<CloudUploadSession> CreateFileUploadAsync(
        AgentRegistration registration, FileUploadSession upload, CancellationToken token);
    Task UploadFileChunkAsync(
        AgentRegistration registration, Guid uploadId, int index, byte[] content,
        CancellationToken token);
    Task CompleteFileUploadAsync(
        AgentRegistration registration, Guid uploadId, CancellationToken token);
    Task CancelFileUploadAsync(
        AgentRegistration registration, Guid uploadId, CancellationToken token);
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
    DesiredState DesiredState,
    IReadOnlyList<Guid> AcceptedProducerMessageIds);
