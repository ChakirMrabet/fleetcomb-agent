using FleetComb.Agent.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FleetComb.Agent.Api;

public sealed class SynchronizationWorker(
    AgentSynchronizationService synchronization,
    ILogger<SynchronizationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await synchronization.RunAsync(
                    result =>
                    {
                        logger.LogInformation(
                            "FleetComb synchronized at {ServerTime}; revision {Revision}.",
                            result.ServerTime, result.DesiredState.Revision);
                        return Task.CompletedTask;
                    },
                    stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (HttpRequestException exception)
            {
                logger.LogWarning(exception, "FleetComb synchronization failed; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }
}
