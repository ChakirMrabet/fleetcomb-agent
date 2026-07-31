namespace FleetComb.Agent.Application.Abstractions;

public interface IAgentStatusNotifier
{
    Task NotifyAsync(string change, CancellationToken cancellationToken);
}
