using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Application.Abstractions;

public interface ILocalAdapterStore
{
    Task<IReadOnlyList<LocalAdapterIdentity>> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(LocalAdapterIdentity identity, CancellationToken cancellationToken);
    Task<LocalAdapterIdentity?> FindByTokenHashAsync(
        string tokenHash, CancellationToken cancellationToken);
}
