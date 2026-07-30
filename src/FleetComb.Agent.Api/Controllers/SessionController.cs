using FleetComb.Agent.Api.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetComb.Agent.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.AgentUi)]
public sealed class SessionController(IAntiforgery antiforgery) : ControllerBase
{
    [HttpPost("/logout")]
    public async Task<IActionResult> Logout()
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        await HttpContext.SignOutAsync(AuthenticationSchemes.AgentUi);
        return Redirect("/Login");
    }
}
