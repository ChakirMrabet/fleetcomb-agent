namespace FleetComb.Agent.Domain;

public sealed record LocalAdapterIdentity(
    Guid Id,
    string Name,
    string Version,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Scopes,
    string TokenHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset? RevokedAt,
    long? AcknowledgedConfigurationRevision = null);

public sealed record LocalAdapterRegistration(
    Guid Id,
    string Name,
    string Version,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Scopes,
    string Token,
    DateTimeOffset CreatedAt);
