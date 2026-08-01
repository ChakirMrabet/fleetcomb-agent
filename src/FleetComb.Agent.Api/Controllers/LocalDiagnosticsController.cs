using FleetComb.Agent.Api.Authentication;
using FleetComb.Agent.Application.Diagnostics.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetComb.Agent.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.LocalApi,
    Policy = "local:status.read")]
[Route("local/v1")]
public sealed class LocalDiagnosticsController(IMediator mediator) : ControllerBase
{
    [HttpGet("protocol")]
    public async Task<IActionResult> Protocol(CancellationToken token) =>
        Ok(await mediator.Send(new GetAdapterDiagnostics.Query(), token));

    [HttpGet("diagnostics")]
    public async Task<IActionResult> Diagnostics(CancellationToken token) =>
        Ok(await mediator.Send(new GetAdapterDiagnostics.Query(), token));
}
