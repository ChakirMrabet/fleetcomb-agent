using FleetComb.Agent.Api.Authentication;
using FleetComb.Agent.Application.Adapters.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetComb.Agent.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.LocalApi)]
[Route("local/v1/adapter")]
public sealed class LocalAdapterController(IMediator mediator) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterAdapterRequest request,
        CancellationToken token)
    {
        try
        {
            return Ok(await mediator.Send(new RegisterAdapter.Command(
                request.Name,
                request.Version,
                request.Capabilities),
                token));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = exception.Message });
        }
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(CancellationToken token)
    {
        var status = await mediator.Send(new RecordAdapterHeartbeat.Command(), token);
        return status is null ? NotFound() : Ok(status);
    }

    public sealed record RegisterAdapterRequest(
        string Name,
        string Version,
        IReadOnlyList<string> Capabilities);
}
