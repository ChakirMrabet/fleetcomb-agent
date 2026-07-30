using FleetComb.Agent.Application.Abstractions;
using FleetComb.Agent.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FleetComb.Agent.Ui.Pages;

[Authorize]
public sealed class IndexModel(
    IAgentRegistrationStore registrations,
    ISoftwareStateStore software) : PageModel
{
    public AgentRegistration? Registration { get; private set; }
    public DesiredState? Desired { get; private set; }
    public IReadOnlyList<ApplicationObservation> Inventory { get; private set; } = [];
    public UpdateStatus Update { get; private set; } = UpdateStatus.Idle();

    public async Task OnGet(CancellationToken cancellationToken)
    {
        Registration = await registrations.LoadAsync(cancellationToken);
        Desired = await software.LoadDesiredAsync(cancellationToken);
        Inventory = await software.LoadInventoryAsync(cancellationToken);
        Update = await software.LoadUpdateStatusAsync(cancellationToken);
    }
}
