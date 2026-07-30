using FleetComb.Agent.Api.Authentication;
using FleetComb.Agent.Application.Status.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetComb.Agent.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.LocalApi)]
[Route("local/v1/status")]
public sealed class LocalStatusController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken token) =>
        Ok(await mediator.Send(new GetLocalAgentStatus.Query(), token));
}
