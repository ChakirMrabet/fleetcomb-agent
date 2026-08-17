using System.Diagnostics;
using System.Net;
using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Application;

public sealed class AgentSynchronizationService(
    IAgentRegistrationStore registrations,
    ISoftwareStateStore software,
    IProducerMessageStore producerMessages,
    IAgentCloudClient cloud,
    IAgentStatusNotifier notifier)
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
                var previousDesired = await software.LoadDesiredAsync(cancellationToken);
                var previousStatus =
                    await software.LoadSynchronizationStatusAsync(cancellationToken);
                var inventory = await software.LoadInventoryAsync(cancellationToken);
                var outbound = await producerMessages.LoadPendingAsync(100, cancellationToken);
                var response = await cloud.HeartbeatAsync(
                    registration, (long)started.Elapsed.TotalSeconds, inventory,
                    outbound,
                    cancellationToken);
                if (response.AcceptedProducerMessageIds.Count > 0)
                    await producerMessages.MarkDeliveredAsync(
                        response.AcceptedProducerMessageIds, DateTimeOffset.UtcNow,
                        cancellationToken);
                await software.SaveDesiredAsync(response.DesiredState, cancellationToken);
                delay = TimeSpan.FromSeconds(Math.Clamp(
                    response.NextHeartbeatSeconds, 15, 3600));
                await software.SaveSynchronizationStatusAsync(
                    new SynchronizationStatus(
                        "Online", attemptedAt, DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow.Add(delay), ""),
                    cancellationToken);
                var desiredChanged = DesiredChanged(previousDesired, response.DesiredState);
                var authorizationChanged = AuthorizationChanged(previousDesired, response.DesiredState);
                if (desiredChanged || previousStatus.State != "Online")
                    await notifier.NotifyAsync(
                        desiredChanged ? "desired-state" : "synchronization",
                        cancellationToken);
                if (authorizationChanged)
                    await notifier.NotifyAsync("authorized-users", cancellationToken);
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
                if (previous.State != state)
                    await notifier.NotifyAsync("synchronization", cancellationToken);
            }
        }
    }

    private static bool DesiredChanged(DesiredState? previous, DesiredState current) =>
        previous is null ||
        previous.Revision != current.Revision ||
        previous.Product?.Id != current.Product?.Id ||
        !string.Equals(
            previous.Product?.PartNumber,
            current.Product?.PartNumber,
            StringComparison.Ordinal) ||
        !string.Equals(previous.Product?.Name, current.Product?.Name, StringComparison.Ordinal);

    private static bool AuthorizationChanged(DesiredState? previous, DesiredState current) =>
        previous?.Authorization?.Revision != current.Authorization?.Revision ||
        (previous?.Authorization is null) != (current.Authorization is null);
}
