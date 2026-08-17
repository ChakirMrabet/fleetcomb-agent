namespace FleetComb.Agent.Api;

public sealed class AuthorizationRosterExpirationWorker(
    FleetComb.Agent.Application.AuthorizationRosterExpirationService service) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        service.RunAsync(stoppingToken);
}
