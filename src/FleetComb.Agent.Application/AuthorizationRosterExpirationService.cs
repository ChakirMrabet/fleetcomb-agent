using System.Security.Cryptography;
using System.Text;
using FleetComb.Agent.Application.Abstractions;

namespace FleetComb.Agent.Application;

public sealed class AuthorizationRosterExpirationService(
    ISoftwareStateStore software,
    IAgentStatusNotifier notifier,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan MaximumCheckInterval = TimeSpan.FromMinutes(1);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        long? revision = null;
        string? activeFingerprint = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            var desired = await software.LoadDesiredAsync(cancellationToken);
            var authorization = desired?.Authorization;
            var now = timeProvider.GetUtcNow();
            var currentFingerprint = authorization is null
                ? null
                : Fingerprint(authorization.Users
                    .Where(user => user.NotAfter > now)
                    .OrderBy(user => user.MembershipId)
                    .Select(user => new { user.MembershipId, user.Username, user.NotAfter }));

            if (authorization?.Revision != revision)
            {
                revision = authorization?.Revision;
                activeFingerprint = currentFingerprint;
            }
            else if (!string.Equals(activeFingerprint, currentFingerprint, StringComparison.Ordinal))
            {
                activeFingerprint = currentFingerprint;
                await notifier.NotifyAsync("authorized-users", cancellationToken);
            }

            var nextExpiration = authorization?.Users
                .Where(user => user.NotAfter > now)
                .Select(user => (DateTimeOffset?)user.NotAfter)
                .Min();
            var delay = nextExpiration is null
                ? MaximumCheckInterval
                : TimeSpan.FromTicks(Math.Clamp(
                    (nextExpiration.Value - now).Ticks,
                    TimeSpan.FromSeconds(1).Ticks,
                    MaximumCheckInterval.Ticks));
            await Task.Delay(delay, timeProvider, cancellationToken);
        }
    }

    private static string Fingerprint<T>(T value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(value))));
}
