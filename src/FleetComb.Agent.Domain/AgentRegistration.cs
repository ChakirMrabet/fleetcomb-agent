namespace FleetComb.Agent.Domain;

public sealed record AgentRegistration(
    Uri ServerUrl,
    Guid TenantId,
    Guid AssetId,
    Guid InstallationId,
    string PrivateKey,
    int HeartbeatIntervalSeconds,
    string LocalApiToken = "");

public sealed record PlatformInformation(
    string Hostname,
    string OsFamily,
    string OsVersion,
    string Architecture);
