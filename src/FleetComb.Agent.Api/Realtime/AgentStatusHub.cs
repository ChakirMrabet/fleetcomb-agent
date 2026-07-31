using FleetComb.Agent.Api.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FleetComb.Agent.Api.Realtime;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.AgentUi)]
public sealed class AgentStatusHub : Hub
{
    public const string Route = "/hubs/status";
}
