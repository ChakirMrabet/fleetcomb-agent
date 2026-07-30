using FleetComb.Agent.Api.Authentication;
using FleetComb.Agent.Application.Updates.Commands;
using FleetComb.Agent.Application.Updates.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetComb.Agent.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.LocalApi)]
[Route("local/v1")]
public sealed class LocalUpdatesController(
    IMediator mediator) : ControllerBase
{
    [HttpGet("updates/current")]
    public async Task<IActionResult> Current(CancellationToken token) =>
        Ok(await mediator.Send(new GetCurrentUpdate.Query(), token));

    [HttpPost("applications/{applicationId:guid}/install")]
    public async Task<IActionResult> Install(
        Guid applicationId,
        CancellationToken token) =>
        Ok(await mediator.Send(new StartApplicationUpdate.Command(applicationId), token));

    [HttpPost("applications/{applicationId:guid}/install-completion")]
    public async Task<IActionResult> Complete(
        Guid applicationId,
        AdapterInstallCompletionRequest request,
        CancellationToken token)
    {
        try
        {
            return Ok(await mediator.Send(new CompleteAdapterInstallation.Command(
                applicationId,
                request.Succeeded,
                request.Message),
                token));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new ProblemDetails { Title = exception.Message });
        }
    }

    public sealed record AdapterInstallCompletionRequest(bool Succeeded, string Message);
}
