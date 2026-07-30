namespace FleetComb.Agent.Application.Abstractions;

public interface IAgentDataReset
{
    Task<string> ResetAsync(CancellationToken cancellationToken);
}
