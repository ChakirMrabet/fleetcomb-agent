using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FleetComb.Agent.Application.Authentication.Commands;
using FleetComb.Agent.Application.Authentication.Queries;
using FleetComb.Agent.Application.Enrollment.Commands;
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
public sealed class EnrollModel(
    IMediator mediator) : PageModel
{
    [BindProperty]
    public EnrollmentInput Input { get; set; } = new();

    public async Task<IActionResult> OnGet(CancellationToken cancellationToken)
    {
        if (await mediator.Send(new GetRegistration.Query(), cancellationToken) is null)
            return Page();
        return await mediator.Send(
            new IsAdministratorConfigured.Query(), cancellationToken)
            ? RedirectToPage("/Login")
            : RedirectToPage("/Setup");
    }

    public async Task<IActionResult> OnPost(CancellationToken cancellationToken)
    {
        if (await mediator.Send(new GetRegistration.Query(), cancellationToken) is not null)
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
            await mediator.Send(
                new EnrollAgent.Command(server, Input.EnrollmentCode), cancellationToken);
            await mediator.Send(
                new SetAdministratorPassword.Command(Input.AdministratorPassword),
                cancellationToken);
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
