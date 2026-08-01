using FleetComb.Agent.Api.Authentication;
using FleetComb.Agent.Application.Adapters.Commands;
using FleetComb.Agent.Application.Adapters;
using FleetComb.Agent.Application.Adapters.Queries;
using System.Security.Claims;
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
    [Authorize(Policy = "local:bootstrap")]
    public async Task<IActionResult> Register(
        RegisterAdapterRequest request,
        CancellationToken token)
    {
        try
        {
            return Ok(await mediator.Send(new RegisterAdapter.Command(
                request.Name,
                request.Version,
                request.Capabilities,
                request.Scopes ?? LocalAdapterScopes.All),
                token));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = exception.Message });
        }
    }

    [HttpPost("heartbeat")]
    [Authorize(Policy = "local:status.read")]
    public async Task<IActionResult> Heartbeat(CancellationToken token)
    {
        var adapterId = Guid.TryParse(
            User.FindFirstValue("adapter_id"), out var parsed) ? parsed : Guid.Empty;
        var status = await mediator.Send(new RecordAdapterHeartbeat.Command(adapterId), token);
        return status is null ? NotFound() : Ok(status);
    }

    public sealed record RegisterAdapterRequest(
        string Name,
        string Version,
        IReadOnlyList<string> Capabilities,
        IReadOnlyList<string>? Scopes);

    [HttpGet]
    [Authorize(Policy = "local:bootstrap")]
    public async Task<IActionResult> List(CancellationToken token) =>
        Ok((await mediator.Send(new GetAdapters.Query(), token))
            .Select(x => new
            {
                x.Id, x.Name, x.Version, x.Capabilities, x.Scopes, x.CreatedAt,
                x.LastSeenAt, x.RevokedAt, x.AcknowledgedConfigurationRevision
            }));

    [HttpDelete("{adapterId:guid}")]
    [Authorize(Policy = "local:bootstrap")]
    public async Task<IActionResult> Revoke(Guid adapterId, CancellationToken token) =>
        await mediator.Send(new RevokeAdapter.Command(adapterId), token) ? NoContent() : NotFound();

    [HttpPost("configuration/acknowledge")]
    [Authorize(Policy = "local:configuration.read")]
    public async Task<IActionResult> Acknowledge(
        AcknowledgeRequest request, CancellationToken token)
    {
        var adapterId = Guid.TryParse(User.FindFirstValue("adapter_id"), out var id)
            ? id : Guid.Empty;
        return await mediator.Send(
            new AcknowledgeConfiguration.Command(adapterId, request.Revision), token)
            ? NoContent()
            : Conflict(new ProblemDetails { Title = "The configuration revision is no longer current." });
    }

    public sealed record AcknowledgeRequest(long Revision);
}
