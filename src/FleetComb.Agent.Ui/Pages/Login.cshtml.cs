using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FleetComb.Agent.Application.Authentication.Queries;
using FleetComb.Agent.Application.Enrollment.Queries;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace FleetComb.Agent.Ui.Pages;

[AllowAnonymous]
[EnableRateLimiting("setup")]
public sealed class LoginModel(IMediator mediator) : PageModel
{
    [BindProperty, Required]
    public string Password { get; set; } = "";

    public async Task<IActionResult> OnGet(CancellationToken cancellationToken)
    {
        if (await mediator.Send(new GetRegistration.Query(), cancellationToken) is null)
            return RedirectToPage("/Enroll");
        return await mediator.Send(
            new IsAdministratorConfigured.Query(), cancellationToken)
            ? Page()
            : RedirectToPage("/Setup");
    }

    public async Task<IActionResult> OnPost(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        if (!await mediator.Send(
                new VerifyAdministratorPassword.Query(Password), cancellationToken))
        {
            ModelState.AddModelError(string.Empty, "The password is incorrect.");
            return Page();
        }
        await HttpContext.SignInAsync(
            "AgentUi",
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "Local administrator")], "AgentUi")));
        return RedirectToPage("/Index");
    }
}
