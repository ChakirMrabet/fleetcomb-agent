using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Application;

public sealed class CustomerAdapterService(ISoftwareStateStore software)
{
    private static readonly TimeSpan OfflineAfter = TimeSpan.FromSeconds(15);

    public async Task<CustomerAdapterStatus> GetStatusAsync(CancellationToken token) =>
        Normalize(await software.LoadAdapterStatusAsync(token), DateTimeOffset.UtcNow);

    public async Task<CustomerAdapterStatus> RegisterAsync(
        string name,
        string version,
        IReadOnlyList<string> capabilities,
        CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(capabilities);
        if (capabilities.Count > 100)
            throw new ArgumentException("At most 100 capabilities may be registered.");

        var status = new CustomerAdapterStatus(
            "Connected",
            name.Trim(),
            version.Trim(),
            capabilities
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            DateTimeOffset.UtcNow);
        await software.SaveAdapterStatusAsync(status, token);
        return status;
    }

    public async Task<CustomerAdapterStatus?> HeartbeatAsync(CancellationToken token)
    {
        var current = await software.LoadAdapterStatusAsync(token);
        if (string.IsNullOrWhiteSpace(current.Name)) return null;

        var updated = current with
        {
            State = "Connected",
            LastSeenAt = DateTimeOffset.UtcNow
        };
        await software.SaveAdapterStatusAsync(updated, token);
        return updated;
    }

    internal static CustomerAdapterStatus Normalize(
        CustomerAdapterStatus status,
        DateTimeOffset now) =>
        status.LastSeenAt.HasValue && now - status.LastSeenAt.Value > OfflineAfter
            ? status with { State = "Offline" }
            : status;
}
