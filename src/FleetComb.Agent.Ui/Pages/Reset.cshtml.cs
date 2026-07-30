using System.ComponentModel.DataAnnotations;
using FleetComb.Agent.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FleetComb.Agent.Ui.Pages;

[Authorize]
public sealed class ResetModel(AgentResetService reset) : PageModel
{
    [BindProperty, Required]
    public string Confirmation { get; set; } = "";

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPost(CancellationToken cancellationToken)
    {
        if (!string.Equals(Confirmation.Trim(), "RESET", StringComparison.Ordinal))
            ModelState.AddModelError(
                nameof(Confirmation), "Type RESET exactly to confirm.");
        if (!ModelState.IsValid) return Page();
        await reset.ResetAsync(cancellationToken);
        await HttpContext.SignOutAsync("AgentUi");
        return RedirectToPage("/Enroll");
    }
}
