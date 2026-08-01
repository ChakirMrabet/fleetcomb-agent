using System.Security.Claims;
using System.Text.Json;
using FleetComb.Agent.Api.Authentication;
using FleetComb.Agent.Application.Telemetry.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetComb.Agent.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.LocalApi,
    Policy = "local:telemetry.write")]
[Route("local/v1")]
[RequestSizeLimit(64 * 1024)]
public sealed class LocalTelemetryController(IMediator mediator) : ControllerBase
{
    [HttpPost("health")]
    public async Task<IActionResult> Health(Submission request, CancellationToken token) =>
        Accepted(await mediator.Send(new ReportHealth.Command(
            AdapterId(), request.Schema, request.Severity, request.Payload), token));

    [HttpPost("events")]
    public async Task<IActionResult> Event(Submission request, CancellationToken token) =>
        Accepted(await mediator.Send(new PublishEvent.Command(
            AdapterId(), request.Schema, request.Severity, request.Payload), token));

    [HttpPost("logs")]
    public async Task<IActionResult> Log(Submission request, CancellationToken token) =>
        Accepted(await mediator.Send(new SubmitLog.Command(
            AdapterId(), request.Schema, request.Severity, request.Payload), token));

    private Guid AdapterId() => Guid.TryParse(
        User.FindFirstValue("adapter_id"), out var id) ? id : Guid.Empty;

    public sealed record Submission(string Schema, string Severity, JsonElement Payload);
}
