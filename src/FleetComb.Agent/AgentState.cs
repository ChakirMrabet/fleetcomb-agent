namespace FleetComb.Agent;

public sealed record AgentState(
    Uri ServerUrl,
    Guid TenantId,
    Guid AssetId,
    Guid InstallationId,
    string PrivateKey,
    int HeartbeatIntervalSeconds,
    string LocalApiToken = "");

public interface IAgentStateStore
{
    Task<AgentState?> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(AgentState state, CancellationToken cancellationToken);
}
