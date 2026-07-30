namespace FleetComb.Agent.Application.Abstractions;

public interface ILocalAdministratorStore
{
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken);
    Task SetPasswordAsync(string password, CancellationToken cancellationToken);
    Task<bool> VerifyPasswordAsync(string password, CancellationToken cancellationToken);
}
