using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FleetComb.Agent;

public sealed class HeartbeatWorker(
    IAgentStateStore stateStore,
    ISoftwareStateStore softwareState,
    AgentApiClient api,
    ILogger<HeartbeatWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var state = await stateStore.LoadAsync(stoppingToken);
        if (state is null)
        {
            logger.LogError(
                "Agent is not enrolled. Run the executable with 'enroll --server URL --code CODE'.");
            return;
        }
        var started = Stopwatch.StartNew();
        var delay = TimeSpan.Zero;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (delay > TimeSpan.Zero) await Task.Delay(delay, stoppingToken);
            try
            {
                var inventory = await softwareState.LoadInventoryAsync(stoppingToken);
                var response = await api.HeartbeatAsync(
                    state, (long)started.Elapsed.TotalSeconds, inventory, stoppingToken);
                await softwareState.SaveDesiredAsync(response.DesiredState, stoppingToken);
                delay = TimeSpan.FromSeconds(Math.Clamp(
                    response.NextHeartbeatSeconds, 15, 3600));
                logger.LogInformation(
                    "Heartbeat accepted at {ServerTime}; next in {DelaySeconds}s.",
                    response.ServerTime, delay.TotalSeconds);
            }
            catch (HttpRequestException exception)
            {
                delay = TimeSpan.FromSeconds(Math.Min(
                    Math.Max(delay.TotalSeconds * 2, 15), 300));
                logger.LogWarning(
                    exception, "Heartbeat failed; retrying in {DelaySeconds}s.",
                    delay.TotalSeconds);
            }
        }
    }
}
