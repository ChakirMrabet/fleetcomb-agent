using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FleetComb.Agent.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace FleetComb.Agent.Ui.Pages;

[AllowAnonymous]
[EnableRateLimiting("setup")]
public sealed class SetupModel(
    IAgentRegistrationStore registrations,
    ILocalAdministratorStore administrator) : PageModel
{
    [BindProperty, Required, MinLength(12)]
    public string Password { get; set; } = "";
    [BindProperty, Required]
    public string ConfirmPassword { get; set; } = "";

    public async Task<IActionResult> OnGet(CancellationToken cancellationToken)
    {
        if (await registrations.LoadAsync(cancellationToken) is null)
            return RedirectToPage("/Enroll");
        return await administrator.IsConfiguredAsync(cancellationToken)
            ? RedirectToPage("/Login")
            : Page();
    }

    public async Task<IActionResult> OnPost(CancellationToken cancellationToken)
    {
        if (await registrations.LoadAsync(cancellationToken) is null)
            return RedirectToPage("/Enroll");
        if (await administrator.IsConfiguredAsync(cancellationToken))
            return RedirectToPage("/Login");
        if (Password != ConfirmPassword)
            ModelState.AddModelError(nameof(ConfirmPassword), "The passwords do not match.");
        if (!ModelState.IsValid) return Page();
        await administrator.SetPasswordAsync(Password, cancellationToken);
        await HttpContext.SignInAsync(
            "AgentUi",
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "Local administrator")], "AgentUi")));
        return RedirectToPage("/Index");
    }
}
