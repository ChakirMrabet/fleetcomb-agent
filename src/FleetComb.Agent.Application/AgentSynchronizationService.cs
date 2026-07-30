using System.Diagnostics;
using FleetComb.Agent.Application.Abstractions;

namespace FleetComb.Agent.Application;

public sealed class AgentSynchronizationService(
    IAgentRegistrationStore registrations,
    ISoftwareStateStore software,
    IAgentCloudClient cloud)
{
    public async Task RunAsync(
        Func<HeartbeatResult, Task>? synchronized,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.StartNew();
        var delay = TimeSpan.Zero;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
            var registration = await registrations.LoadAsync(cancellationToken);
            if (registration is null) return;
            var inventory = await software.LoadInventoryAsync(cancellationToken);
            var response = await cloud.HeartbeatAsync(
                registration, (long)started.Elapsed.TotalSeconds, inventory, cancellationToken);
            await software.SaveDesiredAsync(response.DesiredState, cancellationToken);
            if (synchronized is not null) await synchronized(response);
            delay = TimeSpan.FromSeconds(Math.Clamp(
                response.NextHeartbeatSeconds, 15, 3600));
        }
    }
}
