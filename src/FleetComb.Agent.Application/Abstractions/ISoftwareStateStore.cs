using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Application.Abstractions;

public interface ISoftwareStateStore
{
    Task<DesiredState?> LoadDesiredAsync(CancellationToken cancellationToken);
    Task SaveDesiredAsync(DesiredState desired, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApplicationObservation>> LoadInventoryAsync(
        CancellationToken cancellationToken);
    Task SaveObservationAsync(
        ApplicationObservation observation, CancellationToken cancellationToken);
    Task<UpdateStatus> LoadUpdateStatusAsync(CancellationToken cancellationToken);
    Task SaveUpdateStatusAsync(UpdateStatus status, CancellationToken cancellationToken);
    Task<SynchronizationStatus> LoadSynchronizationStatusAsync(
        CancellationToken cancellationToken);
    Task SaveSynchronizationStatusAsync(
        SynchronizationStatus status, CancellationToken cancellationToken);
    Task<CustomerAdapterStatus> LoadAdapterStatusAsync(CancellationToken cancellationToken);
    Task SaveAdapterStatusAsync(
        CustomerAdapterStatus status, CancellationToken cancellationToken);
}
