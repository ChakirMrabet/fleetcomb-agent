using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FleetComb.Agent.Application;
using FleetComb.Agent.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace FleetComb.Agent.Ui.Pages;

[AllowAnonymous]
[EnableRateLimiting("setup")]
public sealed class EnrollModel(
    EnrollmentService enrollment,
    IAgentRegistrationStore registrations,
    ILocalAdministratorStore administrator) : PageModel
{
    [BindProperty]
    public EnrollmentInput Input { get; set; } = new();

    public async Task<IActionResult> OnGet(CancellationToken cancellationToken)
    {
        if (await registrations.LoadAsync(cancellationToken) is null) return Page();
        return await administrator.IsConfiguredAsync(cancellationToken)
            ? RedirectToPage("/Login")
            : RedirectToPage("/Setup");
    }

    public async Task<IActionResult> OnPost(CancellationToken cancellationToken)
    {
        if (await registrations.LoadAsync(cancellationToken) is not null)
            return RedirectToPage("/Login");
        if (!ModelState.IsValid) return Page();
        if (!Uri.TryCreate(Input.ServerUrl, UriKind.Absolute, out var server))
        {
            ModelState.AddModelError("Input.ServerUrl", "Enter a valid FleetComb address.");
            return Page();
        }
        if (Input.AdministratorPassword != Input.ConfirmPassword)
        {
            ModelState.AddModelError("Input.ConfirmPassword", "The passwords do not match.");
            return Page();
        }
        try
        {
            await enrollment.EnrollAsync(
                server, Input.EnrollmentCode, cancellationToken);
            await administrator.SetPasswordAsync(
                Input.AdministratorPassword, cancellationToken);
            await SignIn();
            return RedirectToPage("/Index");
        }
        catch (Exception exception) when (
            exception is HttpRequestException or ArgumentException or IOException
                or UnauthorizedAccessException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }
    }

    private Task SignIn() => HttpContext.SignInAsync(
        "AgentUi",
        new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "Local administrator")], "AgentUi")));

    public sealed class EnrollmentInput
    {
        [Required, Url]
        public string ServerUrl { get; set; } = "http://localhost:5000";
        [Required]
        public string EnrollmentCode { get; set; } = "";
        [Required, MinLength(12)]
        public string AdministratorPassword { get; set; } = "";
        [Required]
        public string ConfirmPassword { get; set; } = "";
    }
}
