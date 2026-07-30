using FleetComb.Agent.Application.Abstractions;

namespace FleetComb.Agent.Application;

public sealed class AgentResetService(IAgentDataReset reset)
{
    public Task<string> ResetAsync(CancellationToken cancellationToken) =>
        reset.ResetAsync(cancellationToken);
}
