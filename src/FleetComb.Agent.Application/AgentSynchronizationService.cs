using System.Diagnostics;
using System.Net;
using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;

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
            if (registration is null)
            {
                await software.SaveSynchronizationStatusAsync(
                    SynchronizationStatus.NotEnrolled(), cancellationToken);
                return;
            }
            var attemptedAt = DateTimeOffset.UtcNow;
            try
            {
                var inventory = await software.LoadInventoryAsync(cancellationToken);
                var response = await cloud.HeartbeatAsync(
                    registration, (long)started.Elapsed.TotalSeconds, inventory,
                    cancellationToken);
                await software.SaveDesiredAsync(response.DesiredState, cancellationToken);
                delay = TimeSpan.FromSeconds(Math.Clamp(
                    response.NextHeartbeatSeconds, 15, 3600));
                await software.SaveSynchronizationStatusAsync(
                    new SynchronizationStatus(
                        "Online", attemptedAt, DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow.Add(delay), ""),
                    cancellationToken);
                if (synchronized is not null) await synchronized(response);
            }
            catch (HttpRequestException exception)
            {
                delay = TimeSpan.FromSeconds(15);
                var previous = await software.LoadSynchronizationStatusAsync(cancellationToken);
                var state = exception.StatusCode is HttpStatusCode.Unauthorized
                    or HttpStatusCode.Forbidden
                    ? "AuthenticationFailed"
                    : "Offline";
                await software.SaveSynchronizationStatusAsync(
                    new SynchronizationStatus(
                        state, attemptedAt, previous.LastSuccessfulAt,
                        DateTimeOffset.UtcNow.Add(delay), exception.Message),
                    cancellationToken);
            }
        }
    }
}
