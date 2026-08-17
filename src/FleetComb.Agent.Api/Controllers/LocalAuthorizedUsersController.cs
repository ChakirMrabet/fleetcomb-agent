using FleetComb.Agent.Api.Authentication;
using FleetComb.Agent.Application.Status.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetComb.Agent.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.LocalApi)]
[Route("local/v1/authorized-users")]
public sealed class LocalAuthorizedUsersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "local:access.read")]
    public async Task<IActionResult> List(CancellationToken token)
    {
        var roster = await mediator.Send(new GetAuthorizedUsers.Query(), token);
        return roster is null ? NoContent() : Ok(roster);
    }
}
