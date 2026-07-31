using FleetComb.Agent.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace FleetComb.Agent.Api.Realtime;

public sealed class SignalRAgentStatusNotifier(
    IHubContext<AgentStatusHub> hubContext) : IAgentStatusNotifier
{
    public Task NotifyAsync(string change, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("StatusChanged", change, cancellationToken);
}
