using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;
using System.Security.Cryptography;
using System.Text;

namespace FleetComb.Agent.Application;

public sealed class CustomerAdapterService(
    ISoftwareStateStore software,
    ILocalAdapterStore adapterStore,
    IAgentStatusNotifier notifier)
{
    private static readonly TimeSpan OfflineAfter = TimeSpan.FromSeconds(15);

    public async Task<CustomerAdapterStatus> GetStatusAsync(CancellationToken token) =>
        Normalize(await software.LoadAdapterStatusAsync(token), DateTimeOffset.UtcNow);

    public async Task<LocalAdapterRegistration> RegisterAsync(
        string name,
        string version,
        IReadOnlyList<string> capabilities,
        IReadOnlyList<string> scopes,
        CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(capabilities);
        if (capabilities.Count > 100)
            throw new ArgumentException("At most 100 capabilities may be registered.");

        var normalizedScopes = scopes.Distinct(StringComparer.Ordinal).ToArray();
        if (normalizedScopes.Any(scope =>
                !Adapters.LocalAdapterScopes.All.Contains(scope, StringComparer.Ordinal)))
            throw new ArgumentException("An unsupported local API scope was requested.");
        var id = Guid.NewGuid();
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var now = DateTimeOffset.UtcNow;
        var normalizedCapabilities = capabilities
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        await adapterStore.SaveAsync(new LocalAdapterIdentity(
            id, name.Trim(), version.Trim(), normalizedCapabilities, normalizedScopes,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))),
            now, now, null, null), token);

        var status = new CustomerAdapterStatus(
            "Connected",
            name.Trim(),
            version.Trim(),
            normalizedCapabilities, now);
        await software.SaveAdapterStatusAsync(status, token);
        await notifier.NotifyAsync("local-integration", token);
        return new LocalAdapterRegistration(
            id, name.Trim(), version.Trim(), normalizedCapabilities, normalizedScopes,
            rawToken, now);
    }

    public Task<IReadOnlyList<LocalAdapterIdentity>> ListAsync(CancellationToken token) =>
        adapterStore.LoadAsync(token);

    public async Task<bool> RevokeAsync(Guid adapterId, CancellationToken token)
    {
        var identity = (await adapterStore.LoadAsync(token)).SingleOrDefault(x => x.Id == adapterId);
        if (identity is null) return false;
        await adapterStore.SaveAsync(identity with { RevokedAt = DateTimeOffset.UtcNow }, token);
        return true;
    }

    public async Task<bool> AcknowledgeConfigurationAsync(
        Guid adapterId, long revision, CancellationToken token)
    {
        var desired = await software.LoadDesiredAsync(token);
        if (desired is null || desired.Revision != revision) return false;
        var identity = (await adapterStore.LoadAsync(token))
            .SingleOrDefault(x => x.Id == adapterId && x.RevokedAt is null);
        if (identity is null) return false;
        await adapterStore.SaveAsync(
            identity with { AcknowledgedConfigurationRevision = revision }, token);
        return true;
    }

    public async Task<CustomerAdapterStatus?> HeartbeatAsync(CancellationToken token)
        => await HeartbeatAsync(Guid.Empty, token);

    public async Task<CustomerAdapterStatus?> HeartbeatAsync(
        Guid adapterId, CancellationToken token)
    {
        if (adapterId != Guid.Empty)
        {
            var identity = (await adapterStore.LoadAsync(token))
                .SingleOrDefault(item => item.Id == adapterId && item.RevokedAt is null);
            if (identity is null) return null;
            await adapterStore.SaveAsync(
                identity with { LastSeenAt = DateTimeOffset.UtcNow }, token);
        }
        var current = await software.LoadAdapterStatusAsync(token);
        if (string.IsNullOrWhiteSpace(current.Name)) return null;

        var updated = current with
        {
            State = "Connected",
            LastSeenAt = DateTimeOffset.UtcNow
        };
        await software.SaveAdapterStatusAsync(updated, token);
        if (Normalize(current, DateTimeOffset.UtcNow).State == "Offline")
            await notifier.NotifyAsync("local-integration", token);
        return updated;
    }

    internal static CustomerAdapterStatus Normalize(
        CustomerAdapterStatus status,
        DateTimeOffset now) =>
        status.LastSeenAt.HasValue && now - status.LastSeenAt.Value > OfflineAfter
            ? status with { State = "Offline" }
            : status;
}
