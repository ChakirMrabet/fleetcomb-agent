using System.Text.Json;
using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Infrastructure.Persistence;

public sealed class FileProducerMessageStore(IAgentRegistrationStore registrations)
    : IProducerMessageStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<IReadOnlyList<ProducerMessage>> LoadPendingAsync(
        int maximumCount, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return (await ReadCoreAsync(cancellationToken))
                .Where(item => item.DeliveredAt is null).OrderBy(item => item.Sequence)
                .Take(Math.Clamp(maximumCount, 1, 500)).ToArray();
        }
        finally { gate.Release(); }
    }

    public async Task AppendAsync(ProducerMessage message, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var current = await ReadCoreAsync(cancellationToken);
            if (current.Count(item => item.DeliveredAt is null) >= 10_000)
                throw new InvalidOperationException(
                    "The Agent telemetry queue is full; retry after FleetComb reconnects.");
            await WriteCoreAsync(current.Append(message).TakeLast(10_000).ToArray(), cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async Task MarkDeliveredAsync(
        IReadOnlyCollection<Guid> messageIds, DateTimeOffset deliveredAt,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var ids = messageIds.ToHashSet();
            var updated = (await ReadCoreAsync(cancellationToken))
                .Select(item => ids.Contains(item.Id)
                    ? item with { DeliveredAt = deliveredAt, DeliveryAttempts = item.DeliveryAttempts + 1 }
                    : item)
                .Where(item => item.DeliveredAt is null || item.DeliveredAt > deliveredAt.AddDays(-7))
                .ToArray();
            await WriteCoreAsync(updated, cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async Task<ProducerQueueStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var pending = (await ReadCoreAsync(cancellationToken))
                .Where(item => item.DeliveredAt is null).ToArray();
            return new ProducerQueueStatus(
                pending.Length, pending.Sum(item => (long)item.PayloadJson.Length),
                pending.Length == 0 ? null : pending.Min(item => item.CreatedAt));
        }
        finally { gate.Release(); }
    }

    private async Task<ProducerMessage[]> ReadCoreAsync(CancellationToken token)
    {
        var path = Path.Combine(registrations.DataDirectory, "producer-messages.json");
        if (!File.Exists(path)) return [];
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ProducerMessage[]>(stream, JsonOptions, token) ?? [];
    }

    private async Task WriteCoreAsync(ProducerMessage[] values, CancellationToken token)
    {
        Directory.CreateDirectory(registrations.DataDirectory);
        var path = Path.Combine(registrations.DataDirectory, "producer-messages.json");
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, values, JsonOptions, token);
        FileAgentRegistrationStore.Secure(path);
    }
}
