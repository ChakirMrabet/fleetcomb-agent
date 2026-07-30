using FleetComb.Agent.Api.Authentication;
using FleetComb.Agent.Application.Status.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetComb.Agent.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.AgentUi)]
[Route("ui/v1/status")]
public sealed class UiStatusController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken token) =>
        Ok(await mediator.Send(new GetUiAgentStatus.Query(), token));
}
