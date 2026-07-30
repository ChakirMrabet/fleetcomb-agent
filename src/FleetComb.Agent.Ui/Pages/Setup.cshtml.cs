using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FleetComb.Agent.Application.Authentication.Commands;
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
public sealed class SetupModel(IMediator mediator) : PageModel
{
    [BindProperty, Required, MinLength(12)]
    public string Password { get; set; } = "";
    [BindProperty, Required]
    public string ConfirmPassword { get; set; } = "";

    public async Task<IActionResult> OnGet(CancellationToken cancellationToken)
    {
        if (await mediator.Send(new GetRegistration.Query(), cancellationToken) is null)
            return RedirectToPage("/Enroll");
        return await mediator.Send(
            new IsAdministratorConfigured.Query(), cancellationToken)
            ? RedirectToPage("/Login")
            : Page();
    }

    public async Task<IActionResult> OnPost(CancellationToken cancellationToken)
    {
        if (await mediator.Send(new GetRegistration.Query(), cancellationToken) is null)
            return RedirectToPage("/Enroll");
        if (await mediator.Send(
                new IsAdministratorConfigured.Query(), cancellationToken))
            return RedirectToPage("/Login");
        if (Password != ConfirmPassword)
            ModelState.AddModelError(nameof(ConfirmPassword), "The passwords do not match.");
        if (!ModelState.IsValid) return Page();
        await mediator.Send(
            new SetAdministratorPassword.Command(Password), cancellationToken);
        await HttpContext.SignInAsync(
            "AgentUi",
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "Local administrator")], "AgentUi")));
        return RedirectToPage("/Index");
    }
}
