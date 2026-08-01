using FleetComb.Agent.Api.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FleetComb.Agent.Api.Realtime;

[Authorize(AuthenticationSchemes =
    AuthenticationSchemes.AgentUi + "," + AuthenticationSchemes.LocalApi)]
public sealed class AgentStatusHub : Hub
{
    public const string Route = "/hubs/status";

    public override async Task OnConnectedAsync()
    {
        var credentialType = Context.User?.FindFirst("credential_type")?.Value;
        if (credentialType == "adapter" &&
            Context.User?.Claims.Any(claim =>
                claim.Type == "scope" && claim.Value == "events.subscribe") != true)
        {
            Context.Abort();
            return;
        }
        await base.OnConnectedAsync();
    }
}
