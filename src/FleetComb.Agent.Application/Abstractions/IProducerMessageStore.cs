using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Application.Abstractions;

public interface IProducerMessageStore
{
    Task<IReadOnlyList<ProducerMessage>> LoadPendingAsync(
        int maximumCount, CancellationToken cancellationToken);
    Task AppendAsync(ProducerMessage message, CancellationToken cancellationToken);
    Task MarkDeliveredAsync(
        IReadOnlyCollection<Guid> messageIds, DateTimeOffset deliveredAt,
        CancellationToken cancellationToken);
    Task<ProducerQueueStatus> GetStatusAsync(CancellationToken cancellationToken);
}
