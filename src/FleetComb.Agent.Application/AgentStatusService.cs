using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Application;

public sealed class AgentStatusService(
    IAgentRegistrationStore registrations,
    ISoftwareStateStore software,
    CustomerAdapterService adapters)
{
    public async Task<LocalAgentStatus> GetLocalStatusAsync(CancellationToken token)
    {
        var registration = await registrations.LoadAsync(token);
        return new LocalAgentStatus(
            registration is null
                ? null
                : new LocalAgentIdentity(
                    registration.AssetId,
                    registration.InstallationId,
                    registration.ServerUrl),
            await software.LoadDesiredAsync(token),
            await software.LoadInventoryAsync(token),
            await software.LoadUpdateStatusAsync(token),
            await software.LoadSynchronizationStatusAsync(token),
            await adapters.GetStatusAsync(token));
    }

    public async Task<UiAgentStatus> GetUiStatusAsync(CancellationToken token) =>
        new(
            await software.LoadSynchronizationStatusAsync(token),
            await adapters.GetStatusAsync(token),
            await software.LoadUpdateStatusAsync(token),
            await software.LoadInventoryAsync(token));

    public Task<DesiredState?> GetDesiredStateAsync(CancellationToken token) =>
        software.LoadDesiredAsync(token);

    public Task<IReadOnlyList<ApplicationObservation>> GetInventoryAsync(
        CancellationToken token) =>
        software.LoadInventoryAsync(token);

    public Task<UpdateStatus> GetUpdateStatusAsync(CancellationToken token) =>
        software.LoadUpdateStatusAsync(token);

    public Task ReportApplicationAsync(
        Guid applicationId,
        Guid? softwareReleaseId,
        string version,
        CancellationToken token)
    {
        if (applicationId == Guid.Empty)
            throw new ArgumentException("Application ID is required.", nameof(applicationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        return software.SaveObservationAsync(
            new ApplicationObservation(
                applicationId,
                softwareReleaseId,
                version.Trim(),
                "ExternallyReported",
                DateTimeOffset.UtcNow),
            token);
    }
}

public sealed record LocalAgentIdentity(
    Guid AssetId,
    Guid InstallationId,
    Uri ServerUrl);

public sealed record LocalAgentStatus(
    LocalAgentIdentity? Agent,
    DesiredState? DesiredState,
    IReadOnlyList<ApplicationObservation> InstalledApplications,
    UpdateStatus Update,
    SynchronizationStatus Synchronization,
    CustomerAdapterStatus Adapter);

public sealed record UiAgentStatus(
    SynchronizationStatus Synchronization,
    CustomerAdapterStatus Adapter,
    UpdateStatus Update,
    IReadOnlyList<ApplicationObservation> InstalledApplications);
