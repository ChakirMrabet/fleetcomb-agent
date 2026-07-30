using FleetComb.Agent.Domain;

namespace FleetComb.Agent.Application.Abstractions;

public interface IAgentRegistrationStore
{
    string DataDirectory { get; }
    Task EnsureWritableAsync(CancellationToken cancellationToken);
    Task<AgentRegistration?> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(AgentRegistration registration, CancellationToken cancellationToken);
}
