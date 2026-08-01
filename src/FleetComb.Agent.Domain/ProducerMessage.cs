namespace FleetComb.Agent.Domain;

public sealed record ProducerMessage(
    Guid Id,
    Guid AdapterId,
    long Sequence,
    string Kind,
    string Schema,
    string Severity,
    string PayloadJson,
    DateTimeOffset CreatedAt,
    int DeliveryAttempts,
    DateTimeOffset? DeliveredAt);

public sealed record ProducerQueueStatus(
    int PendingCount,
    long PendingBytes,
    DateTimeOffset? OldestPendingAt);
