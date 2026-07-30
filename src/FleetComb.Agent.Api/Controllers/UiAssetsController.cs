using FleetComb.Agent.Ui;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetComb.Agent.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("agent-ui")]
public sealed class UiAssetsController : ControllerBase
{
    [HttpGet("agent.css")]
    public ContentResult Css() =>
        Content(UiAssets.Css, "text/css; charset=utf-8");

    [HttpGet("status.js")]
    public ContentResult StatusJavaScript() =>
        Content(UiAssets.StatusJavaScript, "text/javascript; charset=utf-8");
}
