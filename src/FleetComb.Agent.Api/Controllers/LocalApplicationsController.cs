using FleetComb.Agent.Api.Authentication;
using FleetComb.Agent.Application.Applications.Commands;
using FleetComb.Agent.Application.Applications.Queries;
using FleetComb.Agent.Application.Status.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetComb.Agent.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.LocalApi)]
[Route("local/v1")]
public sealed class LocalApplicationsController(IMediator mediator) : ControllerBase
{
    [HttpGet("desired-state")]
    public async Task<IActionResult> DesiredState(CancellationToken token)
    {
        var desired = await mediator.Send(new GetDesiredState.Query(), token);
        return desired is null ? NoContent() : Ok(desired);
    }

    [HttpGet("applications")]
    public async Task<IActionResult> List(CancellationToken token) =>
        Ok(await mediator.Send(new GetInstalledApplications.Query(), token));

    [HttpPost("applications/report")]
    public async Task<IActionResult> Report(
        ReportApplicationRequest request,
        CancellationToken token)
    {
        if (request.ApplicationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Version))
            return BadRequest();

        try
        {
            await mediator.Send(new ReportInstalledApplication.Command(
                request.ApplicationId,
                request.SoftwareReleaseId,
                request.Version),
                token);
            return Accepted();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = exception.Message });
        }
    }

    public sealed record ReportApplicationRequest(
        Guid ApplicationId,
        Guid? SoftwareReleaseId,
        string Version);
}
