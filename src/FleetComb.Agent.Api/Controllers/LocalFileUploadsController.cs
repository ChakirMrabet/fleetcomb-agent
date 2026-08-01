using System.Security.Claims;
using System.Text.Json;
using FleetComb.Agent.Api.Authentication;
using FleetComb.Agent.Application.Uploads.Commands;
using FleetComb.Agent.Application.Uploads.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetComb.Agent.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.LocalApi, Policy = "local:uploads.write")]
[Route("local/v1/uploads")]
public sealed class LocalFileUploadsController(IMediator mediator) : ControllerBase
{
    [HttpGet("configuration")]
    public async Task<IActionResult> Configuration(CancellationToken token) =>
        Ok(await mediator.Send(new GetFileUploadConfiguration.Query(), token));

    [HttpPost]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> Create(CreateRequest request, CancellationToken token)
    {
        try
        {
            var value = await mediator.Send(new CreateFileUpload.Command(AdapterId(),
                request.LocalPath, request.Category, request.Schema, request.ContentType,
                request.Metadata, request.CapturedAt), token);
            return AcceptedAtAction(nameof(Get), new { uploadId = value.Id }, value);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return BadRequest(new ProblemDetails { Title = exception.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken token) =>
        Ok(await mediator.Send(new GetFileUploads.Query(AdapterId()), token));

    [HttpGet("{uploadId:guid}")]
    public async Task<IActionResult> Get(Guid uploadId, CancellationToken token)
    {
        var value = await mediator.Send(new GetFileUpload.Query(uploadId, AdapterId()), token);
        return value is null ? NotFound() : Ok(value);
    }

    [HttpDelete("{uploadId:guid}")]
    public async Task<IActionResult> Cancel(Guid uploadId, CancellationToken token) =>
        await mediator.Send(new CancelFileUpload.Command(uploadId, AdapterId()), token)
            ? Accepted() : NotFound();

    [HttpPost("{uploadId:guid}/retry")]
    public async Task<IActionResult> Retry(Guid uploadId, CancellationToken token) =>
        await mediator.Send(new RetryFileUpload.Command(uploadId, AdapterId()), token)
            ? Accepted() : Conflict();

    private Guid AdapterId() => Guid.TryParse(User.FindFirstValue("adapter_id"), out var id)
        ? id : Guid.Empty;

    public sealed record CreateRequest(string LocalPath, string Category, string Schema,
        string ContentType, JsonElement Metadata, DateTimeOffset? CapturedAt);
}
