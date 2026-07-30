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
public sealed class LoginModel(
    IAgentRegistrationStore registrations,
    ILocalAdministratorStore administrator) : PageModel
{
    [BindProperty, Required]
    public string Password { get; set; } = "";

    public async Task<IActionResult> OnGet(CancellationToken cancellationToken)
    {
        if (await registrations.LoadAsync(cancellationToken) is null)
            return RedirectToPage("/Enroll");
        return await administrator.IsConfiguredAsync(cancellationToken)
            ? Page()
            : RedirectToPage("/Setup");
    }

    public async Task<IActionResult> OnPost(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        if (!await administrator.VerifyPasswordAsync(Password, cancellationToken))
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
